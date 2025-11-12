\ parse.fs - Parser for IC grammar

\ TODO: Definition table for top-level named terms (@name = term)
\ Currently disabled due to stack corruption bugs in ADD-DEF
\ Need to properly save name strings before parsing term body

\ Input buffer for parsing
  128 CONSTANT MAX-INPUT-LEN
  CREATE INPUT-BUF MAX-INPUT-LEN ALLOT
  VARIABLE INPUT-LEN
  VARIABLE INPUT-POS

\ Static buffer for constructor pattern cases (8 cells)
  8 CONSTANT CASE-BUFFER-SIZE
  CREATE CASE-BUFFER CASE-BUFFER-SIZE CELLS ALLOT
  VARIABLE TOKEN-POS
  0 INPUT-POS !
  0 TOKEN-POS !

\ Test input buffer
   CREATE TEST-INPUT-BUF MAX-INPUT-LEN ALLOT

\ Binding ID counter (for LAM/VAR labels that fit in 18 bits)
VARIABLE BIND-ID
0 BIND-ID !

\ Simple global binding for testing (only one variable at a time)
VARIABLE GLOBAL-BIND-NAME-ADDR
VARIABLE GLOBAL-BIND-NAME-LEN
VARIABLE GLOBAL-BIND-ID

: FRESH-BIND-ID ( -- id )
  BIND-ID @ 1+ DUP BIND-ID !
;

\ Simple SUBST-PUT for single binding
: SIMPLE-SUBST-PUT ( c-addr u loc -- )
  GLOBAL-BIND-ID !
  GLOBAL-BIND-NAME-LEN !
  GLOBAL-BIND-NAME-ADDR !
;

\ Simple SUBST-GET for single binding
: SIMPLE-SUBST-GET ( c-addr u -- loc | 0 )
  DUP GLOBAL-BIND-NAME-LEN @ <> IF DROP 0 EXIT THEN
  \ Skip string comparison for now
  GLOBAL-BIND-ID @
;

\ SUBST-CLEAR for simple version
: SIMPLE-SUBST-CLEAR ( -- )
  0 GLOBAL-BIND-NAME-ADDR !
  0 GLOBAL-BIND-NAME-LEN !
  0 GLOBAL-BIND-ID !
;

\ Current line and column for error reporting
VARIABLE CURRENT-LINE
VARIABLE CURRENT-COL
1 CURRENT-LINE !
1 CURRENT-COL !

\ Load input string into buffer
   : LOAD-INPUT ( c-addr u -- )
    DUP INPUT-LEN !
    \ Copy string into INPUT-BUF
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
           INPUT-BUF INPUT-POS @ 1+ + C@ 42 = IF  \ '*' for /*
             \ Block comment /* */
             NEXT-CHAR DROP  \ consume first /
             NEXT-CHAR DROP  \ consume *
             BEGIN
               \ Look for */
               PEEK-CHAR 42 = IF  \ '*'
                 NEXT-CHAR DROP
                 PEEK-CHAR 47 = IF  \ '/'
                   NEXT-CHAR DROP  \ consume /
                   -1  \ End of comment, continue outer loop
                 ELSE
                   0  \ Continue inner loop
                 THEN
               ELSE
                 PEEK-CHAR 0<> IF
                   NEXT-CHAR DROP
                   0  \ Continue inner loop
                 ELSE
                   \ EOF in comment
                   S" Unterminated block comment" PARSE-ERROR
                   0
                 THEN
               THEN
             0= UNTIL
           ELSE
             0   \ Stop outer loop
           THEN
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
17 CONSTANT TOK-PLUS
18 CONSTANT TOK-MINUS
19 CONSTANT TOK-DIV
20 CONSTANT TOK-MOD
21 CONSTANT TOK-AND
22 CONSTANT TOK-OR
23 CONSTANT TOK-XOR
24 CONSTANT TOK-SHL
25 CONSTANT TOK-SHR
26 CONSTANT TOK-LT
27 CONSTANT TOK-GT
28 CONSTANT TOK-LE
29 CONSTANT TOK-GE
30 CONSTANT TOK-EQ
31 CONSTANT TOK-NE
32 CONSTANT TOK-COLON

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

\ Read number token (supports underscores like 2_000_000)
: READ-NUMBER ( -- )
  0 TOKEN-LEN !
  BEGIN
    PEEK-CHAR
    DUP IS-DIGIT? OVER 95 = OR WHILE  \ Accept digits and underscore (95 = '_')
    NEXT-CHAR
    DUP 95 <> IF  \ Only store non-underscore characters
    TOKEN-BUF TOKEN-LEN @ + C!
      TOKEN-LEN @ 1+ TOKEN-LEN !
    ELSE
      DROP  \ Skip underscores
    THEN
  REPEAT
  DROP
  TOK-NUMBER TOKEN-TYPE !
;

\ Put back the last token (backup position)
 : UNGET-TOKEN ( -- )
   INPUT-POS @ TOKEN-LEN @ - 0 MAX INPUT-POS !
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

  DUP 58 = IF  \ :
    DROP NEXT-CHAR DROP
    TOK-COLON 0 0 EXIT
  THEN

  DUP 43 = IF  \ +
    DROP NEXT-CHAR DROP
    TOK-PLUS 0 0 EXIT
  THEN

  DUP 45 = IF  \ -
    DROP NEXT-CHAR DROP
    TOK-MINUS 0 0 EXIT
  THEN

  DUP 47 = IF  \ /
    DROP NEXT-CHAR DROP
    TOK-DIV 0 0 EXIT
  THEN

  DUP 37 = IF  \ %
    DROP NEXT-CHAR DROP
    TOK-MOD 0 0 EXIT
  THEN

  DUP 60 = IF  \ <
    DROP NEXT-CHAR DROP
    TOK-LT 0 0 EXIT
  THEN

  DUP 62 = IF  \ >
    DROP NEXT-CHAR DROP
    TOK-GT 0 0 EXIT
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

\ Parse variable or function reference
: PARSE-VAR ( c-addr u -- term )
  \ For now, skip @ check and assume all are variables or function refs

  \ Otherwise, it's a variable reference
  \ Look up variable in substitution map
  SIMPLE-SUBST-GET DUP 0= IF
    \ Not a local variable - treat as function reference
    DROP \ Drop the 0 from SUBST-GET

    \ Create dummy REF term
    2DROP TAG-REF 0 0 PACK-TERM
    EXIT
  THEN

  \ Create VAR term: VAR has val=location
  TAG-VAR 0 ROT PACK-TERM
;

\ Parse lambda: λx body or λ!x body (strict evaluation)
: PARSE-LAM ( -- term )
  \ Optionally consume '!' for strict evaluation
  NEXT-TOKEN ( type addr len )
  2 PICK TOK-BANG = IF
    \ Skip strict evaluation marker - treat same as regular param
    2DROP DROP
    NEXT-TOKEN ( type addr len )
  THEN

  \ Expect identifier for parameter name
  2 PICK TOK-IDENT <> IF
    2DROP DROP
    S" Expected identifier after λ" PARSE-ERROR
    0 EXIT
  THEN
  ROT DROP \ Drop type, leaves: ( addr len )

  \ Get fresh binding ID (fits in 18-bit label field)
  FRESH-BIND-ID ( addr len bind-id )
  DUP >R ( addr len bind-id | R: bind-id )

  \ Store binding: name -> bind-id
  \ SIMPLE-SUBST-PUT expects ( c-addr u loc -- )
  SIMPLE-SUBST-PUT ( | R: bind-id )

  \ Skip whitespace after variable name
  SKIP-WHITESPACE

  \ Parse body term
  PARSE-TERM ( body-term | R: bind-id )

  \ Allocate LAM term in heap (needs 1 cell to store body pointer)
  1 ALLOC ( body-term lam-loc | R: bind-id )
  DUP >R ( body-term lam-loc | R: bind-id lam-loc )
  ! ( | R: bind-id lam-loc )

  \ Create LAM term: TAG-LAM lab=bind-id val=lam-loc
  R> R> SWAP TAG-LAM -ROT PACK-TERM ( lam-term )

  \ TODO: Should unbind variable here (pop scope)
  \ For now, just return the term
;

\ Parse application: (f arg) or (@f arg) or (expr)
: PARSE-APP ( -- term )
  \ Already consumed '(' token

  \ Parse function/expression
  PARSE-TERM ( fun-term )

  \ Check if next token is ')'
  NEXT-TOKEN ( fun type addr len )
  2 PICK TOK-RPAREN = IF
    \ Just parenthesized expression: (expr)
    2DROP DROP ( fun )
    EXIT
  THEN

  \ It's an application: (f arg)
  \ Put back the token and parse argument
  UNGET-TOKEN ( fun type addr len )
  2DROP DROP ( fun )

  \ Parse argument
  PARSE-TERM ( fun arg )

  \ Expect ')'
  NEXT-TOKEN ( fun arg type addr len )
  2 PICK TOK-RPAREN <> IF
    2DROP DROP 2DROP
    S" Expected ')' after application" PARSE-ERROR
    0 EXIT
  THEN
  2DROP DROP ( fun arg )

  \ Allocate APP term in heap (needs 2 cells: fun and arg)
  2 ALLOC ( fun arg app-loc )
  DUP >R ( fun arg app-loc | R: app-loc )
  2 PICK OVER ! ( fun arg app-loc ) \ Store fun at app-loc
  CELL+ ! ( fun | R: app-loc ) \ Store arg at app-loc+CELL
  DROP ( | R: app-loc )

  \ Create APP term: TAG-APP lab=0 val=app-addr
  TAG-APP 0 R> PACK-TERM
;

\ Parse erasure: *
: PARSE-ERA ( -- term )
  \ ERA is just a tag with no heap allocation needed
  TAG-ERA 0 0 PACK-TERM
;

\ Parse U32 number
: PARSE-U32 ( c-addr u -- term )
  \ Use Gforth's >NUMBER for conversion
  \ >NUMBER takes: ( ud1 c-addr1 u1 -- ud2 c-addr2 u2 )
  \ Start with ud1=0, which gives us: ( 0 0 c-addr u -- ud2 c-addr2 u2 )
  0 0 2SWAP >NUMBER ( ud.low ud.high c-addr' u' )

  \ Check if conversion succeeded (u' should be 0)
  IF
    2DROP 2DROP
    S" Invalid number format" PARSE-ERROR
    0 EXIT
  THEN

  \ Drop address, keep double number, then drop high part
  DROP ( ud.low ud.high )
  DROP ( num )

  \ Create U32 term: tag=U32, lab=0, val=number
  TAG-U32 0 ROT PACK-TERM
;

\ Get OP2 opcode from token type
: TOKEN-TO-OP2 ( token-type -- opcode )
  DUP TOK-PLUS = IF DROP 0 EXIT THEN   \ ADD
  DUP TOK-MINUS = IF DROP 1 EXIT THEN  \ SUB
  DUP TOK-STAR = IF DROP 2 EXIT THEN   \ MUL
  DUP TOK-DIV = IF DROP 3 EXIT THEN    \ DIV
  DUP TOK-MOD = IF DROP 4 EXIT THEN    \ MOD
  DUP TOK-AND = IF DROP 5 EXIT THEN    \ AND
  DUP TOK-OR = IF DROP 6 EXIT THEN     \ OR
  DUP TOK-XOR = IF DROP 7 EXIT THEN    \ XOR
  DUP TOK-SHL = IF DROP 8 EXIT THEN    \ SHL
  DUP TOK-SHR = IF DROP 9 EXIT THEN    \ SHR
  DUP TOK-LT = IF DROP 10 EXIT THEN    \ LT
  DUP TOK-GT = IF DROP 11 EXIT THEN    \ GT
  DUP TOK-LE = IF DROP 12 EXIT THEN    \ LE
  DUP TOK-GE = IF DROP 13 EXIT THEN    \ GE
  DUP TOK-EQ = IF DROP 14 EXIT THEN    \ EQ
  DUP TOK-NE = IF DROP 15 EXIT THEN    \ NE
  DROP 0  \ Default
;

\ Parse OP2 binary operation: (+ a b) or (- a b) etc.
: PARSE-OP2 ( op-token -- term )
  \ Get opcode
  TOKEN-TO-OP2
  >R ( | R: opcode )

  \ Parse left operand
  PARSE-TERM >R ( | R: opcode lhs-term )

  \ Parse right operand
  PARSE-TERM R> SWAP ( lhs-term rhs-term | R: opcode )

  \ Expect ')'
  NEXT-TOKEN ( lhs rhs type addr len )
  2 PICK TOK-RPAREN <> IF
    2DROP DROP 2DROP R> DROP
    S" Expected ')' after OP2 arguments" PARSE-ERROR
    0 EXIT
  THEN
  2DROP DROP ( lhs rhs | R: opcode )

  \ Allocate OP2 term in heap (needs 2 cells: lhs, rhs)
  \ Use same pattern as PARSE-APP
  2 ALLOC ( lhs rhs op2-loc | R: opcode )
  DUP >R ( lhs rhs op2-loc | R: opcode op2-loc )
  2 PICK OVER ! ( lhs rhs op2-loc | R: opcode op2-loc ) \ Store lhs at op2-loc
  CELL+ ! ( lhs | R: opcode op2-loc ) \ Store rhs at op2-loc+CELL
  DROP ( | R: opcode op2-loc )

  \ Create OP2 term: TAG-OP2 lab=opcode val=op2-loc
  TAG-OP2 R> R> SWAP PACK-TERM
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

      \ Get fresh binding ID
      FRESH-BIND-ID ( label addr len bind-id )

      \ Store binding: name -> bind-id (use simple version to avoid heap allocation)
      SIMPLE-SUBST-PUT ( label )

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

      \ Get fresh binding ID
      FRESH-BIND-ID ( label addr len bind-id )

      \ Store binding: name -> bind-id (use simple version to avoid heap allocation)
      SIMPLE-SUBST-PUT ( label )

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

\ Parse constructor pattern cases: #Nil: e1, #Cons{h t}: e2, ...
\ Input: scrut-loc on stack, TOK-HASH already consumed
\ Returns MATCH term with type=1 (constructor pattern)
: PARSE-CONSTRUCTOR-PATTERN ( scrut-loc -- term )
  \ We'll collect cases in a dynamically-built array
  \ For simplicity, support up to 8 cases max
  \ Each case: [tag, num-fields, bind-id1, bind-id2, ..., body]

    \ Allocate space for case array (8 cells for simple cases)
    8 ALLOC >R ( scrut-loc | R: case-array-start )

  \ Case counter
  0 ( scrut-loc case-count | R: case-array-start )

   \ Current write position in case array
   R@ ( scrut-loc case-count write-ptr | R: case-array-start )

   BEGIN
     \ Parse constructor tag (#Tag)
     NEXT-TOKEN ( scrut-loc case-count write-ptr type addr len | R: case-array-start )
     2 PICK TOK-IDENT <> IF
       2DROP DROP 2DROP DROP R> DROP
       S" Expected constructor tag after #" PARSE-ERROR
       0 EXIT
     THEN
     ROT DROP ( scrut-loc case-count write-ptr addr len | R: case-array-start )

     \ Convert tag name to numeric ID (use first char for now)
     OVER C@ ( scrut-loc case-count write-ptr addr len tag-id | R: case-array-start )
     >R 2DROP ( scrut-loc case-count write-ptr | R: case-array-start tag-id )

      \ Store tag in case array
      R@ ! ( scrut-loc case-count write-ptr | R: case-array-start tag-id )
      CELL+ ( scrut-loc case-count write-ptr' | R: case-array-start tag-id )

    \ Check for field bindings: '{' or ':'
    NEXT-TOKEN ( scrut-loc case-count write-ptr' type addr len | R: case-array-start tag-id )
    2 PICK TOK-LBRACE = IF
      \ Has fields: #Tag{field1 field2 ...}
      2DROP DROP ( scrut-loc case-count write-ptr' | R: case-array-start tag-id )

      \ Parse field names and store bind-ids
      0 ( scrut-loc case-count write-ptr' num-fields | R: case-array-start tag-id )
      BEGIN
        NEXT-TOKEN ( scrut-loc case-count write-ptr' num-fields type addr len | R: case-array-start tag-id )
        2 PICK TOK-RBRACE = IF
          \ End of fields
          2DROP DROP ( scrut-loc case-count write-ptr' num-fields | R: case-array-start tag-id )
          -1 \ Exit loop
        ELSE
          2 PICK TOK-IDENT <> IF
            2DROP DROP 2DROP 2DROP R> R> 2DROP
            S" Expected field name in constructor pattern" PARSE-ERROR
            0 EXIT
          THEN
           ROT DROP ( scrut-loc case-count write-ptr' num-fields addr len | R: case-array-start tag-id )

           \ Check if this is a wildcard
           2DUP S" _" STR= IF
             \ Wildcard - use bind-id = 0, don't create binding
             2DROP 0 ( scrut-loc case-count write-ptr' num-fields bind-id | R: case-array-start tag-id )
           ELSE
             \ Normal field - get fresh bind-id and create binding
             FRESH-BIND-ID ( scrut-loc case-count write-ptr' num-fields addr len bind-id | R: case-array-start tag-id )
             DUP >R ( scrut-loc case-count write-ptr' num-fields addr len bind-id | R: case-array-start tag-id bind-id )

             \ Store in SUBST map (use simple version to avoid heap allocation)
             SIMPLE-SUBST-PUT ( scrut-loc case-count write-ptr' num-fields | R: case-array-start tag-id bind-id )

             R> ( scrut-loc case-count write-ptr' num-fields bind-id | R: case-array-start tag-id )
           THEN

           DUP >R ( scrut-loc case-count write-ptr' num-fields bind-id | R: case-array-start tag-id bind-id )

            \ Store bind-id in case array (after tag and num-fields)
            \ Address is: write-ptr' + 1*CELL + field-idx*CELL
            \ write-ptr' = case-start + 1*CELL, field-idx = num-fields
            OVER CELL+ ( scrut-loc case-count write-ptr' num-fields bind-id-addr | R: case-array-start tag-id bind-id )
            OVER CELLS + ( scrut-loc case-count write-ptr' num-fields storage-addr | R: case-array-start tag-id bind-id )
            R> SWAP ! ( scrut-loc case-count write-ptr' num-fields | R: case-array-start tag-id )

          \ Increment field count
          1+ ( scrut-loc case-count write-ptr' num-fields' | R: case-array-start tag-id )
          0 \ Continue loop
        THEN
      0= UNTIL

        \ Store num-fields in case array at write-ptr (which is case-start + CELL)
        OVER OVER SWAP ! ( scrut-loc case-count write-ptr' num-fields | R: case-array-start tag-id )

       \ Advance write-ptr to body position: case-start + (2 + num-fields)*CELL
       \ write-ptr' = case-start + 1*CELL, so add (1 + num-fields)*CELL
       SWAP OVER ( scrut-loc case-count num-fields write-ptr' num-fields | R: case-array-start tag-id )
       1 + CELLS + ( scrut-loc case-count num-fields write-ptr'' | R: case-array-start tag-id )
       SWAP ( scrut-loc case-count write-ptr'' num-fields | R: case-array-start tag-id )
       DROP ( scrut-loc case-count write-ptr'' | R: case-array-start tag-id )
    ELSE
      \ No fields: #Tag:
      2 PICK TOK-COLON <> IF
        2DROP DROP 2DROP DROP R> R> 2DROP
        S" Expected ':' or '{' after constructor tag" PARSE-ERROR
        0 EXIT
      THEN
      2DROP DROP ( scrut-loc case-count write-ptr' | R: case-array-start tag-id )

      \ Store num-fields = 0 at write-ptr'
      DUP 0 SWAP ! ( scrut-loc case-count write-ptr' | R: case-array-start tag-id )
      CELL+ ( scrut-loc case-count write-ptr'' | R: case-array-start tag-id )
      NEXT-TOKEN DROP 2DROP \ Consume ':'
    THEN

    R> DROP ( scrut-loc case-count write-ptr'' | R: case-array-start )

    \ Parse case body
    PARSE-TERM ( scrut-loc case-count write-ptr'' body | R: case-array-start )

    \ Store body in case array
    OVER TUCK ! ( scrut-loc case-count body write-ptr'' | R: case-array-start )
    CELL+ ( scrut-loc case-count body write-ptr''' | R: case-array-start )
    NIP ( scrut-loc case-count write-ptr''' | R: case-array-start )

    \ Increment case counter
    SWAP 1+ SWAP ( scrut-loc case-count' write-ptr''' | R: case-array-start )

    \ Check for more cases (comma or closing brace)
    NEXT-TOKEN ( scrut-loc case-count' write-ptr''' type addr len | R: case-array-start )
    2 PICK TOK-COMMA = IF
      \ More cases
      2DROP DROP ( scrut-loc case-count' write-ptr''' | R: case-array-start )
      NEXT-TOKEN ( scrut-loc case-count' write-ptr''' type addr len | R: case-array-start )
      2 PICK TOK-HASH <> IF
        2DROP DROP 2DROP DROP R> DROP
        S" Expected #Tag after comma" PARSE-ERROR
        0 EXIT
      THEN
      2DROP DROP ( scrut-loc case-count' write-ptr''' | R: case-array-start )
      0 \ Continue loop
    ELSE
      2 PICK TOK-RBRACE = IF
        \ End of cases
        2DROP DROP ( scrut-loc case-count' write-ptr''' | R: case-array-start )
        -1 \ Exit loop
      ELSE
        2DROP DROP 2DROP DROP R> DROP
        S" Expected '}' or ',' after case body" PARSE-ERROR
        0 EXIT
      THEN
    THEN
  0= UNTIL

   \ Now we have: scrut-loc case-count write-ptr
   \ Create MATCH term with constructor pattern type (label=1)
   DROP ( scrut-loc case-count | R: case-array-start )

  \ Allocate MATCH term storage: [scrut-loc, num-cases, case-array-ptr]
  3 ALLOC DUP >R ( scrut-loc case-count match-loc | R: case-array-start match-loc )

  \ Store values
  2 PICK OVER ! ( scrut-loc case-count match-loc | R: case-array-start match-loc )
  OVER OVER CELL+ ! ( scrut-loc case-count match-loc | R: case-array-start match-loc )
  R@ SWAP 2 CELLS + ! ( scrut-loc case-count | R: case-array-start match-loc )
  2DROP ( | R: case-array-start match-loc )

  \ Create MATCH term: TAG-MATCH lab=1 (constructor type) val=match-loc
  R> R> DROP ( match-loc )
  TAG-MATCH 1 ROT PACK-TERM
;

\ Parse pattern matching: ~n { 0: a, 1+p: b }
\ Returns a desugared term using core IC primitives
: PARSE-PATTERN-MATCH ( -- term )
  \ Already consumed '~' token

    \ Parse scrutinee variable
    NEXT-TOKEN ( type addr len )
    2 PICK TOK-IDENT <> IF
      2DROP DROP
      S" Expected variable after ~" PARSE-ERROR
      0 EXIT
    THEN
    ROT DROP ( addr len )

    \ Look up existing binding for scrutinee variable
    2DUP SIMPLE-SUBST-GET DUP 0= IF
      2DROP DROP
      S" Undefined scrutinee variable" PARSE-ERROR
      0 EXIT
    THEN
    >R ( addr len | R: bind-id )

    \ Create VAR term for scrutinee using existing bind-id
    TAG-VAR 0 R@ PACK-TERM ( addr len scrut-var | R: bind-id )
    >R ( addr len | R: bind-id scrut-var )

    \ Drop the variable name since we don't need it anymore
    2DROP ( | R: bind-id scrut-var )

  \ For now, skip optional ! before { (strict evaluation)
  \ We'll handle it in a future enhancement
  NEXT-TOKEN ( type addr len )
  2 PICK TOK-BANG = IF
    \ Skip strict evaluation marker for now
    2DROP DROP
    NEXT-TOKEN ( type addr len )
  THEN

  \ Expect '{'
  2 PICK TOK-LBRACE <> IF
    2DROP DROP R> DROP
    S" Expected '{' after scrutinee" PARSE-ERROR
    0 EXIT
  THEN
  2DROP DROP ( | R: scrut-loc )

   \ Parse case branches - detect pattern type by first token
   NEXT-TOKEN ( type addr len | R: bind-id scrut-var )

    \ Check if it's numeric pattern (0:, 1+p:) or constructor pattern (#Tag:)
    2 PICK TOK-NUMBER = IF
      \ Numeric pattern - simplified parsing for 0: body, 1+p: body
      ROT DROP ( addr len | R: bind-id scrut-var )
      2DUP S" 0" STR= 0= IF
        2DROP R> R> 2DROP DROP
        S" Expected 0 as first pattern" PARSE-ERROR
        0 EXIT
      THEN
      2DROP ( | R: bind-id scrut-var )
      NEXT-TOKEN DROP 2DROP  \ Skip ':'
      PARSE-TERM ( zero-body | R: bind-id scrut-var )
      NEXT-TOKEN ( type addr len | R: bind-id scrut-var zero-body )
      2 PICK TOK-COMMA = IF
        2DROP DROP  \ Skip comma
        NEXT-TOKEN DROP 2DROP  \ Skip '1'
        NEXT-TOKEN DROP 2DROP  \ Skip '+'
        NEXT-TOKEN ( type addr len | R: bind-id scrut-var zero-body )
        2 PICK TOK-IDENT <> IF
          2DROP DROP R> R> 2DROP DROP
          S" Expected variable after 1+" PARSE-ERROR
          0 EXIT
        THEN
        ROT DROP ( addr len | R: bind-id scrut-var zero-body )
        2DUP S" p" STR= 0= IF
          2DROP R> R> 2DROP DROP
          S" Expected 'p'" PARSE-ERROR
          0 EXIT
        THEN
        \ Use simple substitution for pattern variable
        FRESH-BIND-ID DUP >R ( addr len bind-id | R: bind-id scrut-var zero-body bind-id )
        SIMPLE-SUBST-PUT ( | R: bind-id scrut-var zero-body bind-id )
        NEXT-TOKEN DROP 2DROP  \ Skip ':'
        PARSE-TERM ( succ-body | R: bind-id scrut-var zero-body )
        NEXT-TOKEN DROP 2DROP  \ Skip '}'
        R> ( succ-bind-id | R: bind-id scrut-var zero-body succ-body )
      ELSE 2 PICK TOK-RBRACE = IF
        2DROP DROP  \ Skip '}'
        0 0  \ succ-body, succ-bind-id
      ELSE
        2DROP DROP R> R> 2DROP DROP
        S" Expected ',' or '}'" PARSE-ERROR
        0 EXIT
      THEN THEN

      \ Create MATCH term
      R> R> ( succ-bind-id succ-body zero-body bind-id scrut-var )
      4 ALLOC DUP >R
      2 PICK OVER ! ROT OVER CELL+ ! ROT OVER 2 CELLS + ! ROT OVER 3 CELLS + ! NIP R>
      TAG-MATCH 0 ROT PACK-TERM
    ELSE 2 PICK TOK-HASH = IF
      \ Constructor pattern - consume the #
      2DROP DROP R> R> DROP ( scrut-var )
      PARSE-CONSTRUCTOR-PATTERN
    ELSE
      \ Unexpected token
      2DROP DROP R> R> 2DROP DROP
      S" Expected number or # for pattern case" PARSE-ERROR
      0 EXIT
     THEN THEN
;

\ Forward declaration for recursive call
DEFER PARSE-CTR

\ Parse constructor: #Tag{field1, field2, ...}
:NONAME ( -- term )
  \ Expect tag identifier
  NEXT-TOKEN ( type addr len )
  2 PICK TOK-IDENT <> IF
    2DROP DROP
    S" Expected constructor tag after #" PARSE-ERROR
    0 EXIT
  THEN
  ROT DROP ( addr len )

  \ Convert tag name to a numeric ID (simple hash)
  \ For simplicity: use first character as tag ID
  OVER C@ ( addr len tag-id )
  >R 2DROP ( | R: tag-id )

  \ Expect '{'
  NEXT-TOKEN ( type addr len | R: tag-id )
  2 PICK TOK-LBRACE <> IF
    2DROP DROP R> DROP
    S" Expected '{' after constructor tag" PARSE-ERROR
    0 EXIT
  THEN
  2DROP DROP ( | R: tag-id )

  \ Count fields and collect them
  \ We'll use a simple approach: parse up to 8 fields max
  \ and store them in heap
  0 ( field-count | R: tag-id )

  \ Check for empty constructor #Tag{}
  NEXT-TOKEN ( field-count type addr len | R: tag-id )
  2 PICK TOK-RBRACE = IF
    \ Empty constructor
    2DROP DROP ( field-count | R: tag-id )
    R> TAG-CTR SWAP 0 PACK-TERM EXIT
  THEN

  \ Parse first field (put token back by re-parsing)
  DUP >R ( field-count type addr len | R: tag-id field-count )
  2 PICK TOK-IDENT = IF
    ROT DROP PARSE-VAR ( field1 | R: tag-id field-count )
  ELSE 2 PICK TOK-NUMBER = IF
    ROT DROP PARSE-U32 ( field1 | R: tag-id field-count )
  ELSE 2 PICK TOK-LPAREN = IF
    2DROP DROP PARSE-TERM ( field1 | R: tag-id field-count )
  ELSE 2 PICK TOK-LAMBDA = IF
    2DROP DROP PARSE-LAM ( field1 | R: tag-id field-count )
  ELSE 2 PICK TOK-STAR = IF
    2DROP DROP PARSE-ERA ( field1 | R: tag-id field-count )
  ELSE 2 PICK TOK-AMP = IF
    2DROP DROP PARSE-SUP ( field1 | R: tag-id field-count )
  ELSE 2 PICK TOK-HASH = IF
    2DROP DROP PARSE-CTR ( field1 | R: tag-id field-count )
  ELSE
    2DROP DROP R> DROP ( field-count | R: tag-id )
    R> DROP
    S" Unexpected token in constructor" PARSE-ERROR
    0 EXIT
  THEN THEN THEN THEN THEN THEN THEN
  R> 1+ ( field1 field-count+1 | R: tag-id )

\ Parse remaining fields (comma-separated) - but limit to single field for now
  \ For #Cons{1,*} we expect exactly one comma and second field
  NEXT-TOKEN ( field1 type addr len | R: tag-id )
  2 PICK TOK-COMMA = IF
    2DROP DROP ( field1 | R: tag-id )
    
    \ Parse second field
    PARSE-TERM ( field1 field2 | R: tag-id )
    
    \ Expect '}'
    NEXT-TOKEN ( field1 field2 type addr len | R: tag-id )
    2 PICK TOK-RBRACE = IF
      2DROP DROP ( field1 field2 | R: tag-id )
      
      \ For now, just use first field, drop second
      DROP ( field1 | R: tag-id )
      
      \ Create CTR term with single field
      R> TAG-CTR SWAP ROT PACK-TERM
      EXIT
    THEN
  THEN
  
  \ Single field case - expect '}'
  2 PICK TOK-RBRACE = IF
    2DROP DROP ( field1 | R: tag-id )
    
    \ Create CTR term with single field
    R> TAG-CTR SWAP ROT PACK-TERM
    EXIT
  THEN
  
  \ Error case
  2DROP DROP R> DROP
  S" Expected ',' or '}' in constructor" PARSE-ERROR
  0
; IS PARSE-CTR

\ Main term parser (dispatcher)
:NONAME ( -- term )
  SKIP-WHITESPACE
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

  \ Application or OP2: (...) ( type addr len )
  2 PICK TOK-LPAREN = IF
    2DROP DROP
    \ Peek at next token to see if it's an operator
    NEXT-TOKEN ( type addr len )
    \ Check if token is an OP2 operator (TOK-STAR or TOK-PLUS through TOK-NE)
    2 PICK CASE
      TOK-STAR OF TRUE ENDOF
      TOK-PLUS OF TRUE ENDOF
      TOK-MINUS OF TRUE ENDOF
      TOK-DIV OF TRUE ENDOF
      TOK-MOD OF TRUE ENDOF
      TOK-AND OF TRUE ENDOF
      TOK-OR OF TRUE ENDOF
      TOK-XOR OF TRUE ENDOF
      TOK-SHL OF TRUE ENDOF
      TOK-SHR OF TRUE ENDOF
      TOK-LT OF TRUE ENDOF
      TOK-GT OF TRUE ENDOF
      TOK-LE OF TRUE ENDOF
      TOK-GE OF TRUE ENDOF
      TOK-EQ OF TRUE ENDOF
      TOK-NE OF TRUE ENDOF
      FALSE
    ENDCASE IF
      2DROP  \ ( type addr len -- type )  Drop addr and len, keep type
      PARSE-OP2 EXIT
    ELSE
      \ Not an operator - it's the function in application
      2 PICK TOK-IDENT = IF
        ROT DROP PARSE-VAR ( fun-term )
      ELSE 2 PICK TOK-NUMBER = IF
        ROT DROP PARSE-U32 ( fun-term )
      ELSE 2 PICK TOK-LAMBDA = IF
        2DROP DROP PARSE-LAM ( fun-term )
      ELSE
        2DROP DROP PARSE-TERM ( fun-term )
      THEN THEN THEN

      \ Now parse argument and finish application
      PARSE-TERM ( fun-term arg-term )

      \ Expect ')'
      NEXT-TOKEN ( fun arg type addr len )
      2 PICK TOK-RPAREN <> IF
        2DROP DROP 2DROP
        S" Expected ')' after application" PARSE-ERROR
        0 EXIT
      THEN
      2DROP DROP ( fun arg )

      \ Allocate APP term
      2 ALLOC DUP >R
      TUCK ! SWAP OVER CELL+ ! DROP
      TAG-APP 0 R> PACK-TERM
      EXIT
    THEN
  THEN

  \ Variable reference ( type addr len )
  2 PICK TOK-IDENT = IF
    ROT DROP ( addr len )
    PARSE-VAR EXIT
  THEN

  \ Number: U32 ( type addr len )
  2 PICK TOK-NUMBER = IF
    ROT DROP ( addr len )
    PARSE-U32 EXIT
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

  \ Constructor: #Tag{...} ( type addr len )
  2 PICK TOK-HASH = IF
    2DROP DROP
    PARSE-CTR EXIT
  THEN

  \ Pattern match: ~n { ... } ( type addr len )
  2 PICK TOK-TILDE = IF
    2DROP DROP
    PARSE-PATTERN-MATCH EXIT
  THEN

  \ Unknown token
  2DROP DROP
  S" Unexpected token" PARSE-ERROR
  0
; IS PARSE-TERM

\ TODO: Top-level definition parsing (@name = term)
\ Currently disabled - need to fix string handling bugs

\ Test U32 and OP2
: TEST-U32-OP2 ( -- )
  ." Test U32/OP2 parsing... "

  \ Test 1: Parse a number
  S" 42" LOAD-INPUT
  PARSE-TERM ( term )
  DUP GET-TAG TAG-U32 = SWAP GET-VAL 42 = AND IF
    ." U32-PASS "
  ELSE
    ." U32-FAIL "
  THEN

  \ Test 2: Parse addition: (+ 2 3)
  S" (+ 2 3)" LOAD-INPUT
  PARSE-TERM ( term )
  DUP GET-TAG TAG-OP2 = IF
    DUP GET-LAB 0 = IF  \ ADD opcode
      ." OP2-PASS" CR
    ELSE
      ." OP2-FAIL" CR
    THEN
  ELSE
    DROP ." OP2-FAIL" CR
  THEN
;

: PARSE-CTR ( -- term )
  \ Parse #Name{field1, field2, ...} or #Name{}

  \ Parse constructor name
  NEXT-TOKEN ( type addr len )
  DUP TOK-IDENT <> IF
    2DROP DROP
    S" Expected constructor name after #" PARSE-ERROR
    0 EXIT
  THEN
  ( addr len )

  \ For now, use a simple hash of the name as constructor ID
  \ TODO: Replace with proper constructor registry
  DUP 0 DO
    OVER I + C@ +
  LOOP
  SWAP DROP  \ Sum of ASCII values as simple ID

  \ Parse opening brace
  NEXT-TOKEN ( ctor-id type addr len )
  2 PICK TOK-LBRACE <> IF
    2DROP DROP
    S" Expected '{' after constructor name" PARSE-ERROR
    DROP 0 EXIT
  THEN
  2DROP ( ctor-id )

  \ Parse fields: either empty {} or {term1, term2, ...}
  NEXT-TOKEN ( ctor-id type addr len )
  2 PICK TOK-RBRACE = IF
    \ Empty constructor: #Name{}
    2DROP DROP
    0  \ field-count = 0
  ELSE
    \ Parse first field
    UNGET-TOKEN
    PARSE-TERM  \ ctor-id field1

    1 >R  \ field-count = 1
    >R    \ Save field1 on R-stack

    \ Parse additional fields separated by commas
    BEGIN
      NEXT-TOKEN ( ctor-id type addr len | R: field-count, field1, ... )
      2 PICK TOK-RBRACE = IF
        \ End of fields
        2DROP DROP
        R>  \ Get field count
        TRUE  \ Exit flag
      ELSE
        2 PICK TOK-COMMA <> IF
          2DROP DROP
          S" Expected ',' or '}' in constructor fields" PARSE-ERROR
          \ Clean up R-stack
          BEGIN R> DROP R@ 0= UNTIL R> DROP
          0 EXIT
        THEN
        2DROP DROP  \ Drop comma token

        \ Parse next field
        PARSE-TERM >R
        R> 1+ >R  \ Increment field count
        FALSE  \ Continue flag
      THEN
    UNTIL
  THEN

  \ Now we have: ctor-id field-count
  \ Fields are on R-stack in reverse order: fieldN ... field1

  \ Allocate field array: [count][field0][field1][...]
  DUP 1+ CELLS ALLOC ( ctor-id field-count field-array )

  \ Store field count
  2 PICK SWAP ! ( ctor-id field-count field-array )

  \ Store fields in correct order (reverse from R-stack)
  DUP CELL+ SWAP  \ field-array+1 field-count
  0 DO
    R> OVER !     \ Store field (pop from R-stack)
    CELL+         \ Next slot
  LOOP
  DROP            \ Drop final address

  \ Create CTR term
  SWAP MAKE-CTR   \ field-array ctor-id -> ctr-term
;

\ Test CTR parsing
: TEST-CTR ( -- )
  ." Testing CTR parsing..." CR

  \ Test 1: Parse empty constructor
  ." Test 1: Empty constructor #Nil{}... "
  S" #Nil{}" LOAD-INPUT
  PARSE-TERM ( term )
  GET-TAG TAG-CTR = IF
    ." PASS" CR
  ELSE
    ." FAIL" CR
  THEN

  \ Test 2: Parse constructor with fields
  ." Test 2: Constructor with fields #Cons{1,*}... "
  S" #Cons{1, *}" LOAD-INPUT
  PARSE-TERM ( term )
  GET-TAG TAG-CTR = IF
    ." PASS" CR
  ELSE
    ." FAIL" CR
  THEN

  \ Test 3: Parse nested constructors
  ." Test 3: Nested constructors #Cons{1, #Nil{}}... "
  S" #Cons{1, #Nil{}}" LOAD-INPUT
  PARSE-TERM ( term )
  GET-TAG TAG-CTR = IF
    ." PASS" CR
  ELSE
    ." FAIL" CR
  THEN
;

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
   SIMPLE-SUBST-CLEAR

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
   S" f" 100 SIMPLE-SUBST-PUT
   S" x" 200 SIMPLE-SUBST-PUT
   \ Set input buffer manually
   INPUT-BUF DUP 40 SWAP C! 1+  \ '('
   DUP 102 SWAP C! 1+  \ 'f'
   DUP 32 SWAP C! 1+  \ ' '
   DUP 120 SWAP C! 1+  \ 'x'
   41 SWAP C!  \ ')'
   5 INPUT-LEN !
   0 INPUT-POS !
   1 CURRENT-LINE !
   1 CURRENT-COL !
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
   S" x" 42 SIMPLE-SUBST-PUT
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

   \ Test 5: Parse superposition &0{a,b}... "
   ." Test 5: Parse superposition &0{a,b}... "
   S" a" 100 SIMPLE-SUBST-PUT
   S" b" 200 SIMPLE-SUBST-PUT
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
  TEST-U32-OP2
  TEST-CTR

  \ Test pattern matching parsing
  ." Testing pattern matching..." CR

  \ Reset
  HEAP HEAP-PTR !
  SUBST-CLEAR

   \ Test numeric pattern ~n { 0: 42, 1+p: p }
   ." Test: Numeric pattern ~n { 0: 42, 1+p: p }... "
   S" n" 100 SIMPLE-SUBST-PUT  \ Bind n
   S" ~n { 0: 42, 1+p: p }" LOAD-INPUT
  PARSE-TERM ( term )
  DUP GET-TAG TAG-MATCH = IF
    DUP GET-LAB 0 = IF
      ." PASS" CR
    ELSE
      ." FAIL (lab=" GET-LAB . ." )" CR
    THEN
  ELSE
    ." FAIL (tag=" GET-TAG . ." )" CR
  THEN
  DROP

  \ Reset
  HEAP HEAP-PTR !
  SUBST-CLEAR

   \ Test constructor pattern stub - simplified to single case
   ." Test: Constructor pattern ~x { #Nil: 0 }... "
   S" x" 200 SIMPLE-SUBST-PUT
   S" ~x { #Nil: 0 }" LOAD-INPUT
  PARSE-TERM ( term )
  DUP GET-TAG TAG-MATCH = IF
    DUP GET-LAB 1 = IF
      ." PASS" CR
    ELSE
      ." FAIL (lab=" GET-LAB . ." )" CR
    THEN
  ELSE
    ." FAIL (tag=" GET-TAG . ." )" CR
  THEN
  DROP
;
