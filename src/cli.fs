\ cli.fs - Command-line interface

\ Flags
VARIABLE COMPILED?
VARIABLE STATS?
VARIABLE QUIET?
VARIABLE NORMALIZE?

0 COMPILED? !
0 STATS? !
0 QUIET? !
0 NORMALIZE? !

\ Timing variables
VARIABLE START-TIME
VARIABLE END-TIME

\ Parse arguments from Gforth command line
: PARSE-ARGS ( -- )
  \ For now, we'll set flags via environment or explicit calls
  \ Full CLI parsing would use Gforth's NEXT-ARG
;

\ Load file into INPUT-BUF
: LOAD-FILE ( c-addr u -- )
  R/O OPEN-FILE IF
    DROP S" Failed to open file" PARSE-ERROR EXIT
  THEN

  >R  \ Save file ID on return stack
  INPUT-BUF MAX-INPUT-LEN R@ READ-FILE IF
    R> CLOSE-FILE DROP
    S" Failed to read file" PARSE-ERROR EXIT
  THEN

  \ Set input length
  INPUT-LEN !

  \ Close file
  R> CLOSE-FILE DROP
;

\ Pretty-print a term (recursive version)
DEFER .TERM

: .OP2-NAME ( opcode -- )
  DUP 0 = IF DROP ." +" EXIT THEN
  DUP 1 = IF DROP ." -" EXIT THEN
  DUP 2 = IF DROP ." *" EXIT THEN
  DUP 3 = IF DROP ." /" EXIT THEN
  DUP 4 = IF DROP ." %" EXIT THEN
  DUP 5 = IF DROP ." &" EXIT THEN
  DUP 6 = IF DROP ." |" EXIT THEN
  DUP 7 = IF DROP ." ^" EXIT THEN
  DUP 8 = IF DROP ." <<" EXIT THEN
  DUP 9 = IF DROP ." >>" EXIT THEN
  DUP 10 = IF DROP ." <" EXIT THEN
  DUP 11 = IF DROP ." >" EXIT THEN
  DUP 12 = IF DROP ." <=" EXIT THEN
  DUP 13 = IF DROP ." >=" EXIT THEN
  DUP 14 = IF DROP ." ==" EXIT THEN
  DUP 15 = IF DROP ." !=" EXIT THEN
  DROP ." ?op?"
;

:NONAME ( term -- )
  DUP GET-TAG

  \ LAM: λx.body
  DUP TAG-LAM = IF
    DROP DUP GET-LAB ( term bind-id )
    ." λx" . ." ."
    GET-VAL @ .TERM EXIT
  THEN

  \ APP: (fun arg)
  DUP TAG-APP = IF
    DROP DUP GET-VAL ( term app-addr )
    ." (" DUP @ .TERM SPACE
    CELL+ @ .TERM ." )" EXIT
  THEN

  \ SUP: &L{a,b}
  DUP TAG-SUP = IF
    DROP DUP GET-LAB ( term label )
    ." &" . ." {"
    DUP GET-VAL ( term sup-addr )
    DUP @ .TERM ." ,"
    CELL+ @ .TERM ." }" EXIT
  THEN

  \ DUP: !&L{r,s}=target;cont
  DUP TAG-DUP = IF
    DROP DUP GET-LAB ( term label )
    ." !&" . ." {r,s}="
    DUP GET-VAL ( term dup-addr )
    DUP @ .TERM ." ;"
    CELL+ @ .TERM EXIT
  THEN

  \ ERA: *
  DUP TAG-ERA = IF
    DROP DROP ." *" EXIT
  THEN

  \ VAR: x123
  DUP TAG-VAR = IF
    DROP GET-VAL ." x" . EXIT
  THEN

  \ U32: number
  DUP TAG-U32 = IF
    DROP GET-VAL . EXIT
  THEN

  \ CTR: #Tag{field1,field2}
  DUP TAG-CTR = IF
    DROP DUP GET-LAB ( term tag-id )
    ." #" EMIT ." {"
    GET-VAL ( fields-addr )
    DUP 0= IF
      DROP ." }"
    ELSE
      DUP @ .TERM
      CELL+ @ ." ," .TERM ." }"
    THEN
    EXIT
  THEN

  \ REF: @name
  DUP TAG-REF = IF
    DROP ." @ref" EXIT
  THEN

  \ OP2: (op lhs rhs)
  DUP TAG-OP2 = IF
    DROP DUP GET-LAB ( term opcode )
    ." (" .OP2-NAME SPACE
    GET-VAL ( op2-addr )
    DUP @ .TERM SPACE
    CELL+ @ .TERM ." )" EXIT
  THEN

  \ MATCH: ~x{0: zero_body, 1+p: succ_body}
  DUP TAG-MATCH = IF
    DROP DUP GET-VAL ( term match-addr )
    ." ~x" OVER @ . ." {"  \ Print scrutinee location
    DUP CELL+ @ ( term match-addr zero-body )
    ." 0:" .TERM ." ,"
    OVER 2 CELLS + @ ( term match-addr succ-bind-id )
    ." 1+p" . ." :"
    3 CELLS + @ .TERM ." }" EXIT
  THEN

  DROP DROP ." <unknown>"
; IS .TERM

\ Forward declaration for PRINT-STATS
DEFER PRINT-STATS

\ Run mode
: RUN-FILE ( c-addr u -- )
  \ Load file into INPUT-BUF
  2DUP LOAD-FILE

  QUIET? @ 0= IF
    ." [Loading " 2DUP TYPE ." ]" CR
  THEN
  2DROP

  \ Reset input position
  0 INPUT-POS !
  0 TOKEN-POS !

  \ Parse all definitions
  LOAD-BOOK DROP DROP  \ LOAD-BOOK expects dummy args we don't use

  QUIET? @ 0= IF
    ." [Loaded " BOOK-COUNT @ . ." definitions]" CR
  THEN

  \ Find and run 'main'
  S" main" BOOK-FIND ( arity term )
  SWAP DROP ( term )

  DUP 0= IF
    DROP
    ." Error: No 'main' function found" CR
    EXIT
  THEN

  QUIET? @ 0= IF
    ." [Reducing main...]" CR
  THEN

  \ Reset iteration counter and start timer
  0 ITR-COUNT !
  UTIME DROP START-TIME !

  \ Reduce to WHNF or full normalization
  NORMALIZE? @ IF
    NORMALIZE ( result )
  ELSE
    WHNF ( result )
  THEN

  \ Stop timer
  UTIME DROP END-TIME !

  \ Print result
  QUIET? @ 0= IF
    ." Result: " DUP .TERM CR
  THEN
  DROP

  \ Print statistics if requested
  PRINT-STATS
;

\ Statistics display
:NONAME ( -- )
  STATS? @ IF
    CR
    ." ────────────────────────────" CR
    ." WORK: " ITR-COUNT @ DUP . ." interactions" CR

    \ Calculate elapsed time in microseconds
    END-TIME @ START-TIME @ - ( elapsed-us )
    DUP 0> IF
      \ Convert to seconds (as float approximation)
      DUP 1000000 / ( elapsed-us seconds )

      \ Calculate MIPS: interactions / seconds / 1000000
      SWAP ( seconds elapsed-us )
      ITR-COUNT @ SWAP ( seconds itr elapsed-us )
      / ( seconds itr-per-us )

      ." TIME: " OVER . ." s" CR
      ." MIPS: " . CR
    ELSE
      DROP
      ." TIME: <1 us" CR
    THEN

    ." ────────────────────────────" CR
  THEN
; IS PRINT-STATS

\ Set statistics flag
: -s ( -- )
  -1 STATS? !
;

\ Set quiet flag
: -Q ( -- )
  -1 QUIET? !
;

\ Set normalize flag
: -N ( -- )
  -1 NORMALIZE? !
;

\ Main entry point for running a file
: RUN ( -- )
  \ Usage: S" filename.hvm" RUN
  \ This expects filename on stack
  RUN-FILE
;

\ Main entry point
: MAIN ( -- )
  PARSE-ARGS
  ." ForthVM CLI ready" CR
  ." Use: S" 34 EMIT ."  file.hvm" 34 EMIT ."  RUN" CR
;

\ Help text
: .HELP ( -- )
  ." Usage: fvm run <file.hvm> [-C] [-s] [-Q] [-N]" CR
  ."   -C  Compiled mode (not yet implemented)" CR
  ."   -s  Show statistics" CR
  ."   -Q  Quiet mode (minimal output)" CR
  ."   -N  Full normalization (reduce inside lambdas)" CR
  CR
  ." In Gforth:" CR
  ."   -s                    \ Enable stats" CR
  ."   -N                    \ Enable normalization" CR
  ."   S" 34 EMIT ."  file.hvm" 34 EMIT ."  RUN      \ Run file" CR
;

\ Test word
: TEST-CLI ( -- )
  ." CLI module loaded" CR
;
