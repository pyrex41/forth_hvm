\ book.fs - Book loading and function dictionary

\ Function dictionary (256 cells = 64 entries of 4 cells each)
\ Each entry: name-addr, name-len, arity, term-ptr
CREATE BOOK-DICT 256 CELLS ALLOT
VARIABLE BOOK-COUNT  \ Number of functions in dictionary
0 BOOK-COUNT !

\ Name buffer for storing function names during parsing
\ (TOKEN-BUF gets reused, so we need a separate buffer)
CREATE NAME-BUF 256 ALLOT
VARIABLE NAME-LEN  \ Length of current function name

\ Maximum number of book entries
64 CONSTANT MAX-BOOK-ENTRIES

\ Get address of nth entry in book dictionary
: BOOK-ENTRY@ ( n -- addr )
  4 CELLS * BOOK-DICT +
;

\ Store function in book
: BOOK-PUT ( name-addr name-len arity term -- )
  BOOK-COUNT @ MAX-BOOK-ENTRIES >= IF
    DROP DROP DROP DROP
    S" Book dictionary full" PARSE-ERROR
    EXIT
  THEN

  \ Save term and arity on R-stack
  >R >R ( name-addr name-len | R: term arity )

  \ Allocate heap space for function name and copy it
  \ This allows multiple functions without NAME-BUF collision
  DUP CELL+ CELL 1- / ( name-addr name-len cells-needed | R: term arity )
  ALLOC ( name-addr name-len name-copy-addr | R: term arity )

  \ Copy the name to allocated space
  \ CMOVE expects: ( src dest len -- )
  \ Stack: ( name-addr name-len name-copy-addr )
  2 PICK OVER 3 PICK CMOVE ( name-addr name-len name-copy-addr | R: term arity )

  \ Rearrange to ( name-copy-addr name-len )
  ROT DROP SWAP ( name-copy-addr name-len | R: term arity )

  \ Restore arity and term
  R> R> ( name-copy-addr name-len arity term )

  \ Get book entry location
  BOOK-COUNT @ BOOK-ENTRY@ ( name-copy-addr name-len arity term entry-addr )

  \ Store: [name-addr] [name-len] [arity] [term-value]
  >R ( name-copy-addr name-len arity term | R: entry-addr )
  R@ 3 CELLS + !  ( name-copy-addr name-len arity | R: entry-addr ) \ Store term value directly
  R@ 2 CELLS + !  ( name-copy-addr name-len | R: entry-addr ) \ Store arity
  R@ CELL+ !      ( name-copy-addr | R: entry-addr ) \ Store name-len
  R> !            ( ) \ Store name-copy-addr

  \ Increment count
  1 BOOK-COUNT +!
;

\ Compare two strings for equality
: STR= ( addr1 len1 addr2 len2 -- flag )
  \ Check if lengths match
  ROT OVER <> IF  \ Brings len1 to compare with len2
    \ Lengths don't match: stack is ( addr1 addr2 len2 )
    DROP 2DROP FALSE EXIT
  THEN

  \ Lengths match: stack is ( addr1 addr2 len )
  \ Compare characters
  0 ?DO
    OVER I + C@ OVER I + C@ <> IF
      2DROP FALSE UNLOOP EXIT
    THEN
  LOOP
  2DROP TRUE
;

\ Find function in book
: BOOK-FIND ( name-addr name-len -- arity term | 0 0 )
  BOOK-COUNT @ 0 ?DO
    2DUP ( name-addr name-len name-addr name-len )
    I BOOK-ENTRY@ ( name-addr name-len name-addr name-len entry-addr )
    DUP >R ( name-addr name-len name-addr name-len entry-addr | R: entry-addr )
    DUP @ SWAP CELL+ @ ( name-addr name-len name-addr name-len entry-name-addr entry-name-len | R: entry-addr )
    STR= IF  ( name-addr name-len | R: entry-addr )
      2DROP ( | R: entry-addr )
      R@ 2 CELLS + @ ( arity | R: entry-addr )
      R> 3 CELLS + @ ( arity term-value )
      UNLOOP EXIT
    ELSE
      R> DROP ( name-addr name-len )
    THEN
  LOOP

  \ Not found
  2DROP 0 0
;

\ Parse a top-level definition: name = term
: PARSE-DEF ( -- flag )
  \ Returns: TRUE if definition parsed, FALSE if EOF or error

  \ Get first token (should be identifier or EOF)
  NEXT-TOKEN ( type addr len )

  \ Check for EOF
  2 PICK TOK-EOF = IF
    2DROP DROP FALSE EXIT
  THEN

  \ Must be identifier
  2 PICK TOK-IDENT <> IF
    2DROP DROP
    S" Expected function name" PARSE-ERROR
    FALSE EXIT
  THEN

  \ Save function name - copy to NAME-BUF since TOKEN-BUF will be reused
  ROT DROP ( addr len ) \ Drop type
  DUP NAME-LEN ! ( addr len ) \ Save length
  \ CMOVE expects ( src dest len )
  OVER NAME-BUF ROT CMOVE ( addr )
  DROP ( )

  \ Expect '=' token
  NEXT-TOKEN ( type addr len )
  2 PICK TOK-EQUALS <> IF
    2DROP DROP
    S" Expected '=' after function name" PARSE-ERROR
    FALSE EXIT
  THEN
  2DROP DROP ( )

  \ Parse the term
  PARSE-TERM ( term )

  \ Store in book dictionary (arity = 0 for now)
  \ Stack should be: ( name-addr name-len arity term )
  NAME-BUF NAME-LEN @ 0 ( term name-addr name-len arity )
  3 ROLL ( name-addr name-len arity term )
  BOOK-PUT

  TRUE  \ Success
;

\ Resolve a single REF term to its definition
:NONAME ( ref-term -- resolved-term )
  DUP GET-TAG TAG-REF <> IF
    \ Not a REF, return as-is
    EXIT
  THEN

  \ Get the name from the REF term
  GET-VAL ( ref-loc )
  DUP @ SWAP CELL+ @ ( name-addr name-len )


  \ Look up in book dictionary
  BOOK-FIND ( arity term )
  SWAP DROP ( term )

  \ Check if found
  DUP 0= IF
    DROP
    S" Undefined function reference" PARSE-ERROR
    TAG-ERA 0 0 PACK-TERM  \ Return ERA on error
  THEN
; IS RESOLVE-REF

\ Forward declaration for recursion
DEFER LINK-TERM

\ Walk a term and resolve all REF nodes recursively
:NONAME ( term -- resolved-term )
  DUP GET-TAG

  \ If it's a REF, resolve it
  DUP TAG-REF = IF
    DROP RESOLVE-REF EXIT
  THEN

  \ If it's LAM, resolve body
  DUP TAG-LAM = IF
    DROP DUP GET-LAB ( term lab )
    OVER GET-VAL @ ( term lab body )
    LINK-TERM ( term lab body' )
    1 ALLOC DUP >R ! ( term lab | R: new-loc )
    R> TAG-LAM -ROT PACK-TERM
    NIP EXIT
  THEN

  \ If it's APP, resolve both fun and arg
  DUP TAG-APP = IF
    DROP DUP GET-VAL ( term app-loc )
    DUP @ LINK-TERM ( term app-loc fun' )
    OVER CELL+ @ LINK-TERM ( term app-loc fun' arg' )
    2 ALLOC DUP >R ( term app-loc fun' arg' | R: new-loc )
    TUCK ! SWAP R@ CELL+ ! ( term app-loc | R: new-loc )
    2DROP R> TAG-APP 0 ROT PACK-TERM EXIT
  THEN

  \ If it's SUP, resolve both branches
  DUP TAG-SUP = IF
    DROP DUP GET-LAB ( term lab )
    OVER GET-VAL ( term lab sup-loc )
    DUP @ LINK-TERM ( term lab sup-loc a' )
    OVER CELL+ @ LINK-TERM ( term lab sup-loc a' b' )
    2 ALLOC DUP >R ( term lab sup-loc a' b' | R: new-loc )
    TUCK ! SWAP R@ CELL+ ! ( term lab sup-loc | R: new-loc )
    2DROP R> TAG-SUP -ROT PACK-TERM
    NIP EXIT
  THEN

  \ If it's DUP, resolve target and continuation
  DUP TAG-DUP = IF
    DROP DUP GET-LAB ( term lab )
    OVER GET-VAL ( term lab dup-loc )
    DUP @ LINK-TERM ( term lab dup-loc target' )
    OVER CELL+ @ LINK-TERM ( term lab dup-loc target' cont' )
    2 ALLOC DUP >R ( term lab dup-loc target' cont' | R: new-loc )
    TUCK ! SWAP R@ CELL+ ! ( term lab dup-loc | R: new-loc )
    2DROP R> TAG-DUP -ROT PACK-TERM
    NIP EXIT
  THEN

  \ For VAR, ERA, U32, CTR, OP2, MATCH - no children to resolve, return as-is
  DROP ( term )
; IS LINK-TERM

\ Link unresolved references (second pass)
\ Walk all book entries and resolve @name references
: LINK-REFS ( -- )
  BOOK-COUNT @ 0 ?DO
    \ Get the term for entry I
    I BOOK-ENTRY@ ( entry-addr )
    DUP >R ( entry-addr | R: entry-addr )
    3 CELLS + @ ( term | R: entry-addr )

    \ Resolve all references in this term
    LINK-TERM ( term' | R: entry-addr )

    \ Store back the resolved term
    R> 3 CELLS + ! ( | R: )
  LOOP
;

\ Main entry point
: LOAD-BOOK ( filename-addr len -- )
  \ For now, we'll load from INPUT-BUF instead of file
  \ TODO: Implement file loading using Gforth's OPEN-FILE

  \ Clear book dictionary
  0 BOOK-COUNT !

  \ Parse all definitions
  BEGIN
    PARSE-DEF  \ Returns FALSE on EOF or error
  WHILE
  REPEAT

  \ Second pass: link references
  LINK-REFS
;

\ Test word
: TEST-BOOK ( -- )
  ." Book module loaded" CR

  \ Test 1: Store and retrieve a function
  ." Test 1: BOOK-PUT and BOOK-FIND... "

  \ Create a simple term (ERA)
  TAG-ERA 0 0 PACK-TERM ( era-term )

  \ Store as "test" function
  S" test" 2DUP >R >R ( name-addr name-len era-term | R: len addr )
  ROT ROT 1 ROT ( name-addr name-len 1 era-term )
  BOOK-PUT ( | R: name-len name-addr )

  \ Look it up
  R> R> BOOK-FIND ( arity term )

  \ Check results
  SWAP 1 = SWAP GET-TAG TAG-ERA = AND IF
    ." PASS" CR
  ELSE
    ." FAIL" CR
  THEN

  \ Clear book for next test
  0 BOOK-COUNT !

  \ Test 2: Parse a simple definition
  ." Test 2: PARSE-DEF simple function... "
  S" main = *" LOAD-INPUT
  PARSE-DEF IF
    \ Check if "main" was stored
    S" main" BOOK-FIND ( arity term )
    SWAP 0 = SWAP GET-TAG TAG-ERA = AND IF
      ." PASS" CR
    ELSE
      ." FAIL" CR
    THEN
  ELSE
    ." FAIL (parse error)" CR
  THEN

  \ Clear book
  0 BOOK-COUNT !

  \ Test 3: Parse and link function references
  ." Test 3: Parse function with reference... "
  S" id = * main = @id" LOAD-INPUT

  \ Parse both definitions
  PARSE-DEF DROP  \ Parse "id = *"
  PARSE-DEF DROP  \ Parse "main = @id"

  \ Before linking, main should have a REF term
  S" main" BOOK-FIND SWAP DROP ( main-term )
  DUP GET-TAG TAG-REF = IF
    \ Now link references
    LINK-REFS

    \ After linking, main should have ERA term (from id)
    S" main" BOOK-FIND SWAP DROP ( main-term' )
    GET-TAG TAG-ERA = IF
      ." PASS" CR
    ELSE
      ." FAIL (not ERA after linking)" CR
    THEN
  ELSE
    ." FAIL (not REF before linking)" CR
  THEN

  \ Clear book
  0 BOOK-COUNT !
;
