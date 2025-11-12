\ cli.fs - Command-line interface

\ Forward declarations
\ (none needed)



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

\ Help text
  : .HELP ( -- )
    ." ForthVM v0.1.0 - HVM3 in Forth" CR
    CR
    ." Usage: fvm run <file.hvm> [options]" CR
    CR
    ." Options:" CR
    ."   -s, --stats     Show performance statistics" CR
    ."   -q, -Q          Quiet mode (minimal output)" CR
    ."   -n, -N          Full normalization (reduce inside lambdas)" CR
    ."   -C              Compiled mode (not yet implemented)" CR
    ."   -h, --help      Show this help" CR
    CR
    ." Examples:" CR
    ."   fvm run test.hvm" CR
    ."   fvm run bench.hvm -s" CR
    ."   fvm run program.hvm -q -n" CR
    CR
   ." In Gforth REPL:" CR
     ."   -s -N" CR
     \ ."   S\" file.hvm\" RUN" CR
  ;

 \ Parse arguments from Gforth command line
  : PARSE-ARGS ( -- filename-addr filename-len | 0 )
    \ Parse command line arguments using Gforth's NEXT-ARG
    \ Returns filename if found, 0 otherwise
    0 0 ( filename-addr filename-len -- initially 0 )

    BEGIN
      NEXT-ARG DUP 0<> WHILE
      ( filename-addr filename-len arg-addr arg-len )

      \ Check for flags
      2DUP S" -s" STR= IF
        2DROP -1 STATS? !
      ELSE 2DUP S" --stats" STR= IF
        2DROP -1 STATS? !
      ELSE 2DUP S" -q" STR= IF
        2DROP -1 QUIET? !
      ELSE 2DUP S" -Q" STR= IF
        2DROP -1 QUIET? !
      ELSE 2DUP S" -n" STR= IF
        2DROP -1 NORMALIZE? !
      ELSE 2DUP S" -N" STR= IF
        2DROP -1 NORMALIZE? !
      ELSE 2DUP S" --normalize" STR= IF
        2DROP -1 NORMALIZE? !
      ELSE 2DUP S" -C" STR= IF
        2DROP -1 COMPILED? !
      ELSE 2DUP S" -h" STR= IF
        2DROP .HELP 0 0
      ELSE 2DUP S" --help" STR= IF
        2DROP .HELP 0 0
      ELSE
        \ If not a flag, it's the filename
        >R >R 2DROP R> R>
      THEN THEN THEN THEN THEN THEN THEN THEN THEN THEN
    REPEAT
    DROP
  ;

\ Load file into INPUT-BUF
  : LOAD-FILE ( c-addr u -- flag )
    \ Returns TRUE on success, FALSE on failure
    \ Clear substitution map from any previous file
    SIMPLE-SUBST-CLEAR

    DEBUG? @ IF ." [DEBUG] Loading file: " 2DUP TYPE CR THEN

    R/O OPEN-FILE IF
      DROP 2DROP S" Could not open file" FILE-ERROR
      FALSE EXIT
    THEN

    >R  \ Save file ID
    TEST-INPUT-BUF MAX-INPUT-LEN R@ READ-FILE IF
      R> CLOSE-FILE DROP
      2DROP S" Could not read file" FILE-ERROR
      FALSE EXIT
    THEN

    \ Set input length
    INPUT-LEN !

    \ Set INPUT-BUF to TEST-INPUT-BUF
    TEST-INPUT-BUF INPUT-BUF !

    \ Close file
    R> CLOSE-FILE DROP
    TRUE
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

   \ CTR: #Tag{field1,field2,...}
   DUP TAG-CTR = IF
     DROP DUP CTR-CONSTRUCTOR-ID ( ctr-id )
     ." #" . ." {"
     DUP CTR-FIELD-COUNT ( ctr-id field-count )
     DUP 0> IF
       0 ?DO
         DUP I CTR-FIELD .TERM
         I OVER 1- < IF ." ," THEN
       LOOP
     THEN
     DROP ." }" EXIT
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
    \ Buffers are allocated at startup

    \ Try to load the file
   2DUP LOAD-FILE IF
     \ File loading failed, fall back to test string
     2DROP
      S" main = 42" LOAD-INPUT
     QUIET? @ 0= IF ." [Loading test string - file loading failed]" CR THEN
   ELSE
     QUIET? @ 0= IF ." [Loaded file successfully]" CR THEN
   THEN

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
       \ Convert to seconds (integer division)
       DUP 1000000 / DUP ( elapsed-us seconds seconds )
       DUP 0= IF
         \ Less than 1 second
         DROP DUP ( elapsed-us elapsed-us )
         ." TIME: " . ." us" CR
         ." MIPS: <0.001" CR
       ELSE
         \ At least 1 second
         ." TIME: " DUP . ." s" CR

         \ Calculate MIPS: interactions / seconds / 1000000
         ITR-COUNT @ SWAP / ( interactions-per-second )
         1000000 / ( mips )
         ." MIPS: " . CR
       THEN
     ELSE
       DROP
       ." TIME: <1 us" CR
       ." MIPS: N/A" CR
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

\ Test suite runner
 : RUN-TESTS ( -- )
   QUIET? @ 0= IF
     ." Running test suite..." CR
     ." ──────────────────────" CR
   THEN

   \ Test files to run
   0 ( pass-count )

   \ Test 1: test_simple.hvm (if it exists)
   S" ../test_simple_num.hvm" RUN-FILE DROP 1+

   \ Test 2: test_match.hvm
   S" ../test_programs/test_match.hvm" RUN-FILE DROP 1+

   \ Test 3: test_constructor_pattern.hvm
   S" ../test_programs/test_constructor_pattern.hvm" RUN-FILE DROP 1+

   \ Test 4: sum_list.hvm
   S" ../test_programs/sum_list.hvm" RUN-FILE DROP 1+

   QUIET? @ 0= IF
     CR ." Test suite completed: " . ." tests run" CR
   THEN
 ;

\ Test command
 : TEST ( -- )
   \ Usage: TEST (runs test suite)
   RUN-TESTS
 ;

\ Main entry point
 : MAIN ( -- )
   PARSE-ARGS ( filename-addr filename-len | 0 )

   DUP 0<> IF
     \ We have a filename
     RUN-FILE
   ELSE
     DROP
     \ No filename provided
     QUIET? @ 0= IF
       .HELP
     THEN
   THEN
 ;



\ Test word
: TEST-CLI ( -- )
  ." CLI module loaded" CR
;
