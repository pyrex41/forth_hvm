\ parse.fs - Parser for IC grammar

\ TODO: Definition table for top-level named terms (@name = term)
\ Currently disabled due to stack corruption bugs in ADD-DEF
\ Need to properly save name strings before parsing term body

\ Input buffer for parsing
CREATE INPUT-BUF 4096 ALLOT
VARIABLE INPUT-LEN
VARIABLE INPUT-POS
0 INPUT-POS !

\ Current line and column for error reporting
VARIABLE CURRENT-LINE
VARIABLE CURRENT-COL
1 CURRENT-LINE !
1 CURRENT-COL !

\ Load input string into buffer
: LOAD-INPUT ( c-addr u -- )
  DUP INPUT-LEN !
  INPUT-BUF SWAP CMOVE
  0 INPUT-POS !
  1 CURRENT-LINE !
  1 CURRENT-COL !
;

\ Check if at end of input
: END-OF-INPUT? ( -- flag )
  INPUT-POS @ INPUT-LEN @ >=
;

\ Peek at current character
: PEEK-CHAR ( -- c )
  END-OF-INPUT? IF
    0
  ELSE
    INPUT-BUF INPUT-POS @ + C@
  THEN
;

\ Consume and return current character
: NEXT-CHAR ( -- c )
  PEEK-CHAR
  DUP 0<> IF
    INPUT-POS @ 1+ INPUT-POS !
    DUP 10 = IF  \ newline
      CURRENT-LINE @ 1+ CURRENT-LINE !
      1 CURRENT-COL !
    ELSE
      CURRENT-COL @ 1+ CURRENT-COL !
    THEN
  THEN
;

\ Check if character is whitespace
: IS-WHITESPACE? ( c -- flag )
  DUP 32 = SWAP   \ space
  DUP 9 = SWAP    \ tab
  DUP 10 = SWAP   \ newline
  13 = OR OR OR   \ carriage return
;

\ Check if character is alphabetic
: IS-ALPHA? ( c -- flag )
  DUP 65 >= OVER 90 <= AND  \ A-Z
  SWAP DUP 97 >= SWAP 122 <= AND  \ a-z
  OR
;

\ Check if character is digit
: IS-DIGIT? ( c -- flag )
  DUP 48 >= SWAP 57 <= AND  \ 0-9
;

\ Check if character is identifier character
: IS-IDENT-CHAR? ( c -- flag )
  DUP IS-ALPHA?
  OVER IS-DIGIT? OR
  SWAP DUP 95 = SWAP  \ underscore
  DUP 64 = SWAP       \ @
  36 = OR OR OR       \ $
;

\ Skip whitespace and comments
: SKIP-WHITESPACE ( -- )
  BEGIN
    \ Skip whitespace
    BEGIN
      PEEK-CHAR DUP IS-WHITESPACE? WHILE
      DROP NEXT-CHAR DROP
    REPEAT
    DROP

    \ Check for line comment //
    PEEK-CHAR 47 = IF  \ '/'
      INPUT-POS @ 1+ INPUT-LEN @ < IF
        INPUT-BUF INPUT-POS @ 1+ + C@ 47 = IF  \ second '/'
          \ Skip to end of line
          NEXT-CHAR DROP  \ consume first /
          NEXT-CHAR DROP  \ consume second /
          BEGIN
            PEEK-CHAR DUP 10 <> SWAP 0<> AND WHILE
            NEXT-CHAR DROP
          REPEAT
          DROP
          -1  \ Continue outer loop
        ELSE
          0   \ Stop outer loop
        THEN
      ELSE
        0  \ Stop outer loop
      THEN
    ELSE
      0  \ Stop outer loop
    THEN
  0= UNTIL
;

\ Token types
0 CONSTANT TOK-EOF
1 CONSTANT TOK-IDENT
2 CONSTANT TOK-LAMBDA
3 CONSTANT TOK-LPAREN
4 CONSTANT TOK-RPAREN
5 CONSTANT TOK-EQUALS
6 CONSTANT TOK-LBRACE
7 CONSTANT TOK-RBRACE
8 CONSTANT TOK-AMP
9 CONSTANT TOK-BANG
10 CONSTANT TOK-TILDE
11 CONSTANT TOK-HASH
12 CONSTANT TOK-STAR
13 CONSTANT TOK-NUMBER
14 CONSTANT TOK-DOT
15 CONSTANT TOK-COMMA
16 CONSTANT TOK-SEMI

\ Token buffer (stores current token text)
CREATE TOKEN-BUF 256 ALLOT
VARIABLE TOKEN-LEN
VARIABLE TOKEN-TYPE

\ Read identifier token
: READ-IDENT ( -- )
  0 TOKEN-LEN !
  BEGIN
    PEEK-CHAR DUP IS-IDENT-CHAR? WHILE
    NEXT-CHAR
    TOKEN-BUF TOKEN-LEN @ + C!
    TOKEN-LEN @ 1+ TOKEN-LEN !
  REPEAT
  DROP
  TOK-IDENT TOKEN-TYPE !
;

\ Read number token
: READ-NUMBER ( -- )
  0 TOKEN-LEN !
  BEGIN
    PEEK-CHAR DUP IS-DIGIT? WHILE
    NEXT-CHAR
    TOKEN-BUF TOKEN-LEN @ + C!
    TOKEN-LEN @ 1+ TOKEN-LEN !
  REPEAT
  DROP
  TOK-NUMBER TOKEN-TYPE !
;

\ Get next token
: NEXT-TOKEN ( -- type addr len )
  SKIP-WHITESPACE

  END-OF-INPUT? IF
    TOK-EOF 0 0 EXIT
  THEN

  PEEK-CHAR

  \ Check for single-character tokens
  DUP 40 = IF  \ (
    DROP NEXT-CHAR DROP
    TOK-LPAREN 0 0 EXIT
  THEN

  DUP 41 = IF  \ )
    DROP NEXT-CHAR DROP
    TOK-RPAREN 0 0 EXIT
  THEN

  DUP 61 = IF  \ =
    DROP NEXT-CHAR DROP
    TOK-EQUALS 0 0 EXIT
  THEN

  DUP 123 = IF  \ {
    DROP NEXT-CHAR DROP
    TOK-LBRACE 0 0 EXIT
  THEN

  DUP 125 = IF  \ }
    DROP NEXT-CHAR DROP
    TOK-RBRACE 0 0 EXIT
  THEN

  DUP 38 = IF  \ &
    DROP NEXT-CHAR DROP
    TOK-AMP 0 0 EXIT
  THEN

  DUP 33 = IF  \ !
    DROP NEXT-CHAR DROP
    TOK-BANG 0 0 EXIT
  THEN

  DUP 126 = IF  \ ~
    DROP NEXT-CHAR DROP
    TOK-TILDE 0 0 EXIT
  THEN

  DUP 35 = IF  \ #
    DROP NEXT-CHAR DROP
    TOK-HASH 0 0 EXIT
  THEN

  DUP 42 = IF  \ *
    DROP NEXT-CHAR DROP
    TOK-STAR 0 0 EXIT
  THEN

  DUP 46 = IF  \ . (use as lambda)
    DROP NEXT-CHAR DROP
    TOK-LAMBDA 0 0 EXIT
  THEN

  DUP 44 = IF  \ ,
    DROP NEXT-CHAR DROP
    TOK-COMMA 0 0 EXIT
  THEN

  DUP 59 = IF  \ ;
    DROP NEXT-CHAR DROP
    TOK-SEMI 0 0 EXIT
  THEN

  \ Check for identifier
  DUP IS-ALPHA? OVER 64 = OR IF  \ letter or @
    DROP
    READ-IDENT
    TOKEN-TYPE @ TOKEN-BUF TOKEN-LEN @
    EXIT
  THEN

  \ Check for number
  DUP IS-DIGIT? IF
    DROP
    READ-NUMBER
    TOKEN-TYPE @ TOKEN-BUF TOKEN-LEN @
    EXIT
  THEN

  \ Unknown character - error
  DROP
  S" Unexpected character" PARSE-ERROR
  TOK-EOF 0 0
;

\ Forward declaration for recursive parsing
DEFER PARSE-TERM

\ Parse variable reference
: PARSE-VAR ( c-addr u -- term )
  \ Look up variable in substitution map
  SUBST-GET DUP 0= IF
    DROP
    S" Undefined variable" PARSE-ERROR
    0 EXIT
  THEN

  \ Create VAR term: VAR has val=location
  \ TAG-VAR 0 location PACK-TERM
  TAG-VAR 0 ROT PACK-TERM
;

\ Parse lambda: λx body
: PARSE-LAM ( -- term )
  \ Expect identifier for parameter name
  NEXT-TOKEN ( type addr len )
  2 PICK TOK-IDENT <> IF
    2DROP DROP
    S" Expected identifier after λ" PARSE-ERROR
    0 EXIT
  THEN
  ROT DROP \ Drop type, leaves: ( addr len )

  \ Allocate location for this binding
  3 ALLOC ( addr len loc )

  \ Store binding: name -> location
  \ SUBST-PUT expects ( c-addr u loc -- )
  SUBST-PUT ( -- )

  \ Parse body term
  PARSE-TERM ( body-term )

  \ Allocate LAM term in heap (needs 1 cell to store body pointer)
  1 ALLOC ( body-term lam-loc )
  DUP >R ( body-term lam-loc | R: lam-loc )
  ! ( | R: lam-loc )

  \ Create LAM term: TAG-LAM lab=0 val=lam-addr
  TAG-LAM 0 R> PACK-TERM ( lam-term )

  \ TODO: Should unbind variable here (pop scope)
  \ For now, just return the term
;

\ Parse application: (f arg) or (@f arg)
: PARSE-APP ( -- term )
  \ Already consumed '(' token

  \ Parse function
  PARSE-TERM ( fun-term )

  \ Parse argument
  PARSE-TERM ( fun-term arg-term )

  \ Expect ')'
  NEXT-TOKEN ( fun arg type addr len )
  2 PICK TOK-RPAREN <> IF
    2DROP DROP 2DROP
    S" Expected ')' after application" PARSE-ERROR
    0 EXIT
  THEN
  2DROP DROP 2DROP ( fun arg )

  \ Allocate APP term in heap (needs 2 cells: fun and arg)
  2 ALLOC ( fun arg app-loc )
  DUP >R ( fun arg app-loc | R: app-loc )
  TUCK ! ( fun app-loc | R: app-loc )
  CELL+ ! ( | R: app-loc )

  \ Create APP term: TAG-APP lab=0 val=app-addr
  TAG-APP 0 R> PACK-TERM
;

\ Parse erasure: *
: PARSE-ERA ( -- term )
  \ ERA is just a tag with no heap allocation needed
  TAG-ERA 0 0 PACK-TERM
;

\ Parse superposition: &label{term1,term2}
: PARSE-SUP ( -- term )
  \ Already consumed '&' token

  \ Expect label (number)
  NEXT-TOKEN ( type addr len )
  2 PICK TOK-NUMBER <> IF
    2DROP DROP
    S" Expected label number after &" PARSE-ERROR
    0 EXIT
  THEN

  \ Convert label string to number
  ROT DROP ( addr len )
  0 SWAP 0 DO
    OVER I + C@ 48 - ( addr acc digit )
    SWAP 10 * + ( addr acc )
  LOOP
  NIP ( label )

  \ Expect '{'
  NEXT-TOKEN ( label type addr len )
  2 PICK TOK-LBRACE <> IF
    2DROP DROP DROP
    S" Expected '{' after & label" PARSE-ERROR
    0 EXIT
  THEN
  2DROP DROP ( label )

  \ Parse first term
  PARSE-TERM ( label term1 )

  \ Expect ','
  NEXT-TOKEN ( label term1 type addr len )
  2 PICK TOK-COMMA <> IF
    2DROP DROP 2DROP
    S" Expected ',' in superposition" PARSE-ERROR
    0 EXIT
  THEN
  2DROP DROP ( label term1 )

  \ Parse second term
  PARSE-TERM ( label term1 term2 )

  \ Expect '}'
  NEXT-TOKEN ( label term1 term2 type addr len )
  2 PICK TOK-RBRACE <> IF
    2DROP DROP 2DROP DROP
    S" Expected '}' after superposition" PARSE-ERROR
    0 EXIT
  THEN
  2DROP DROP ( label term1 term2 )

  \ Allocate SUP term in heap (needs 2 cells: term1 and term2)
  2 ALLOC ( label term1 term2 sup-loc )
  DUP >R ( label term1 term2 sup-loc | R: sup-loc )
  TUCK ! ( label term1 sup-loc | R: sup-loc )
  CELL+ ! ( label | R: sup-loc )

  \ Create SUP term: TAG-SUP lab=label val=sup-addr
  TAG-SUP SWAP R> PACK-TERM
;

\ Parse duplication: ! &label{var1,var2} = term; continuation
: PARSE-DUP ( -- term )
  \ Already consumed '!' token

  \ Expect '&'
  NEXT-TOKEN ( type addr len )
  2 PICK TOK-AMP <> IF
    2DROP DROP
    S" Expected '&' after !" PARSE-ERROR
    0 EXIT
  THEN
  2DROP DROP

  \ Expect label (number)
  NEXT-TOKEN ( type addr len )
  2 PICK TOK-NUMBER <> IF
    2DROP DROP
    S" Expected label number in duplication" PARSE-ERROR
    0 EXIT
  THEN
  ROT DROP ( addr len )
  0 SWAP 0 DO
    OVER I + C@ 48 - ( addr acc digit )
    SWAP 10 * + ( addr acc )
  LOOP
  NIP ( label )

  \ Expect '{'
  NEXT-TOKEN ( label type addr len )
  2 PICK TOK-LBRACE <> IF
    2DROP DROP DROP
    S" Expected '{' in duplication" PARSE-ERROR
    0 EXIT
  THEN
  2DROP DROP ( label )

  \ Expect first variable name
  NEXT-TOKEN ( label type addr len )
  2 PICK TOK-IDENT <> IF
    2DROP DROP DROP
    S" Expected variable name in duplication" PARSE-ERROR
    0 EXIT
  THEN
  ROT DROP ( label addr len )

  \ Allocate location for first binding
  3 ALLOC ( label addr len loc1 )
  SUBST-PUT ( label )

  \ Expect ','
  NEXT-TOKEN ( label type addr len )
  2 PICK TOK-COMMA <> IF
    2DROP DROP DROP
    S" Expected ',' in duplication" PARSE-ERROR
    0 EXIT
  THEN
  2DROP DROP ( label )

  \ Expect second variable name
  NEXT-TOKEN ( label type addr len )
  2 PICK TOK-IDENT <> IF
    2DROP DROP DROP
    S" Expected second variable name in duplication" PARSE-ERROR
    0 EXIT
  THEN
  ROT DROP ( label addr len )

  \ Allocate location for second binding
  3 ALLOC ( label addr len loc2 )
  SUBST-PUT ( label )

  \ Expect '}'
  NEXT-TOKEN ( label type addr len )
  2 PICK TOK-RBRACE <> IF
    2DROP DROP DROP
    S" Expected '}' after duplication variables" PARSE-ERROR
    0 EXIT
  THEN
  2DROP DROP ( label )

  \ Expect '='
  NEXT-TOKEN ( label type addr len )
  2 PICK TOK-EQUALS <> IF
    2DROP DROP DROP
    S" Expected '=' in duplication" PARSE-ERROR
    0 EXIT
  THEN
  2DROP DROP ( label )

  \ Parse duplicated term
  PARSE-TERM ( label dup-term )

  \ Expect ';'
  NEXT-TOKEN ( label dup-term type addr len )
  2 PICK TOK-SEMI <> IF
    2DROP DROP 2DROP
    S" Expected ';' after duplication term" PARSE-ERROR
    0 EXIT
  THEN
  2DROP DROP ( label dup-term )

  \ Parse continuation
  PARSE-TERM ( label dup-term cont-term )

  \ Allocate DUP term in heap (needs 2 cells: dup-term and cont-term)
  2 ALLOC ( label dup-term cont-term dup-loc )
  DUP >R ( label dup-term cont-term dup-loc | R: dup-loc )
  TUCK ! ( label dup-term dup-loc | R: dup-loc )
  CELL+ ! ( label | R: dup-loc )

  \ Create DUP term: TAG-DUP lab=label val=dup-addr
  TAG-DUP SWAP R> PACK-TERM

  \ TODO: Should unbind variables here (pop scope)
;

\ Main term parser (dispatcher)
:NONAME ( -- term )
  NEXT-TOKEN ( type addr len )

  \ Check for EOF ( type addr len )
  2 PICK TOK-EOF = IF
    2DROP DROP
    S" Unexpected end of input" PARSE-ERROR
    0 EXIT
  THEN

  \ Lambda: λx body ( type addr len )
  2 PICK TOK-LAMBDA = IF
    2DROP DROP
    PARSE-LAM EXIT
  THEN

  \ Application: (...) ( type addr len )
  2 PICK TOK-LPAREN = IF
    2DROP DROP
    PARSE-APP EXIT
  THEN

  \ Variable reference ( type addr len )
  2 PICK TOK-IDENT = IF
    ROT DROP ( addr len )
    PARSE-VAR EXIT
  THEN

  \ Erasure: * ( type addr len )
  2 PICK TOK-STAR = IF
    2DROP DROP
    PARSE-ERA EXIT
  THEN

  \ Superposition: &label{...} ( type addr len )
  2 PICK TOK-AMP = IF
    2DROP DROP
    PARSE-SUP EXIT
  THEN

  \ Duplication: ! &label{...} = ...; ... ( type addr len )
  2 PICK TOK-BANG = IF
    2DROP DROP
    PARSE-DUP EXIT
  THEN

  \ Unknown token
  2DROP DROP
  S" Unexpected token" PARSE-ERROR
  0
; IS PARSE-TERM

\ TODO: Top-level definition parsing (@name = term)
\ Currently disabled - need to fix string handling bugs

\ Test tokenizer
: TEST-TOKENIZER ( -- )
  ." Testing tokenizer..." CR

  \ Test 1: Simple identifier
  ." Test 1: Identifier... "
  S" main" LOAD-INPUT
  NEXT-TOKEN ( type addr len )
  2DROP                \ Drop addr len
  TOK-IDENT = IF
    ." PASS" CR
  ELSE
    ." FAIL" CR
  THEN

  \ Test 2: Equals sign
  ." Test 2: Equals sign... "
  S" =" LOAD-INPUT
  NEXT-TOKEN ( type addr len )
  2DROP                \ Drop addr len
  TOK-EQUALS = IF
    ." PASS" CR
  ELSE
    ." FAIL" CR
  THEN

  \ Test 3: Multiple tokens
  ." Test 3: Multiple tokens... "
  S" @main = x" LOAD-INPUT
  NEXT-TOKEN 2DROP     \ @main (TOK-IDENT)
  TOK-IDENT = >R
  NEXT-TOKEN 2DROP     \ =
  TOK-EQUALS = R> AND >R
  NEXT-TOKEN 2DROP     \ x
  TOK-IDENT = R> AND IF
    ." PASS" CR
  ELSE
    ." FAIL" CR
  THEN

  \ Test 4: Parens and whitespace
  ." Test 4: Parens and whitespace... "
  S" (  @f  x  )" LOAD-INPUT
  NEXT-TOKEN 2DROP TOK-LPAREN = >R
  NEXT-TOKEN 2DROP TOK-IDENT = R> AND >R
  NEXT-TOKEN 2DROP TOK-IDENT = R> AND >R
  NEXT-TOKEN 2DROP TOK-RPAREN = R> AND IF
    ." PASS" CR
  ELSE
    ." FAIL" CR
  THEN

  \ Test 5: Comments
  ." Test 5: Comments... "
  S" x // comment" LOAD-INPUT
  NEXT-TOKEN 2DROP TOK-IDENT = >R
  NEXT-TOKEN 2DROP TOK-EOF = R> AND IF
    ." PASS" CR
  ELSE
    ." FAIL" CR
  THEN
;

\ Test parser
: TEST-PARSER ( -- )
  ." Testing parser..." CR

  \ Reset heap and substitution map
  HEAP HEAP-PTR !
  SUBST-CLEAR

  \ Enable debug for parsing
  \ -1 DEBUG? !

  \ Test 1: Parse identity function .x x (using '.' for lambda)
  ." Test 1: Parse identity .x x... "
  S" .x x" LOAD-INPUT
  PARSE-TERM ( term )
  DUP GET-TAG TAG-LAM = IF
    ." PASS" CR
  ELSE
    ." FAIL (tag=" GET-TAG . ." )" CR
  THEN
  DROP

  \ Reset for next test
  HEAP HEAP-PTR !
  SUBST-CLEAR

  \ Test 2: Parse application (f x)
  ." Test 2: Parse application (f x)... "
  \ First bind f and x to make them valid
  S" f" 100 SUBST-PUT
  S" x" 200 SUBST-PUT
  S" (f x)" LOAD-INPUT
  PARSE-TERM ( term )
  DUP GET-TAG TAG-APP = IF
    ." PASS" CR
  ELSE
    ." FAIL (tag=" GET-TAG . ." )" CR
  THEN
  DROP

  \ Reset for next test
  HEAP HEAP-PTR !
  SUBST-CLEAR

  \ Test 3: Parse variable reference
  ." Test 3: Parse variable x... "
  S" x" 42 SUBST-PUT
  S" x" LOAD-INPUT
  PARSE-TERM ( term )
  DUP GET-TAG TAG-VAR = IF
    ." PASS" CR
  ELSE
    ." FAIL (tag=" GET-TAG . ." )" CR
  THEN
  DROP

  \ Reset
  HEAP HEAP-PTR !
  SUBST-CLEAR

  \ Test 4: Parse erasure *
  ." Test 4: Parse erasure *... "
  S" *" LOAD-INPUT
  PARSE-TERM ( term )
  DUP GET-TAG TAG-ERA = IF
    ." PASS" CR
  ELSE
    ." FAIL (tag=" GET-TAG . ." )" CR
  THEN
  DROP

  \ Reset
  HEAP HEAP-PTR !
  SUBST-CLEAR

  \ Test 5: Parse superposition &0{a,b}
  ." Test 5: Parse superposition &0{a,b}... "
  S" a" 100 SUBST-PUT
  S" b" 200 SUBST-PUT
  S" &0{a,b}" LOAD-INPUT
  PARSE-TERM ( term )
  DUP GET-TAG TAG-SUP = IF
    ." PASS" CR
  ELSE
    ." FAIL (tag=" GET-TAG . ." )" CR
  THEN
  DROP

  \ Reset
  HEAP HEAP-PTR !
  SUBST-CLEAR

  \ Test 6: Parse duplication ! &0{x,y} = *; x
  ." Test 6: Parse duplication ! &0{x,y} = *; x... "
  S" ! &0{x,y} = *; x" LOAD-INPUT
  PARSE-TERM ( term )
  DUP GET-TAG TAG-DUP = IF
    ." PASS" CR
  ELSE
    ." FAIL (tag=" GET-TAG . ." )" CR
  THEN
  DROP

  \ Reset
  HEAP HEAP-PTR !
  SUBST-CLEAR
;

\ Test word
: TEST-PARSE ( -- )
  ." Parse module loaded" CR
  ." Token types defined: EOF=" TOK-EOF . ." IDENT=" TOK-IDENT . ." LAMBDA=" TOK-LAMBDA . CR
  TEST-TOKENIZER
  TEST-PARSER
;
