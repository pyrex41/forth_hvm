\ cli.fs - Command-line interface

\ Flags
VARIABLE COMPILED?
VARIABLE STATS?
VARIABLE QUIET?

0 COMPILED? !
0 STATS? !
0 QUIET? !

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

\ Pretty-print a term (simple version)
: .TERM ( term -- )
  DUP GET-TAG

  \ LAM
  DUP TAG-LAM = IF
    DROP ." (.x <body>)" EXIT
  THEN

  \ APP
  DUP TAG-APP = IF
    DROP ." (<fun> <arg>)" EXIT
  THEN

  \ SUP
  DUP TAG-SUP = IF
    DROP DUP GET-LAB ." &" . ." {<a>,<b>}" EXIT
  THEN

  \ DUP
  DUP TAG-DUP = IF
    DROP DUP GET-LAB ." !&" . ." {<r>,<s>}=<t>;K" EXIT
  THEN

  \ ERA
  DUP TAG-ERA = IF
    DROP DROP ." *" EXIT
  THEN

  \ VAR
  DUP TAG-VAR = IF
    DROP DUP GET-VAL ." x" . EXIT
  THEN

  \ U32
  DUP TAG-U32 = IF
    DROP GET-VAL . EXIT
  THEN

  \ CTR
  DUP TAG-CTR = IF
    DROP ." #<ctr>" EXIT
  THEN

  \ REF
  DUP TAG-REF = IF
    DROP ." @<ref>" EXIT
  THEN

  \ OP2
  DUP TAG-OP2 = IF
    DROP ." <op2>" EXIT
  THEN

  DROP DROP ." <unknown>"
;

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

  \ Reduce to WHNF
  WHNF ( result )

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
: PRINT-STATS ( -- )
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
;

\ Set statistics flag
: -s ( -- )
  -1 STATS? !
;

\ Set quiet flag
: -Q ( -- )
  -1 QUIET? !
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
  ." Use: S\" file.hvm\" RUN" CR
;

\ Help text
: .HELP ( -- )
  ." Usage: fvm run <file.hvm> [-C] [-s] [-Q]" CR
  ."   -C  Compiled mode (not yet implemented)" CR
  ."   -s  Show statistics" CR
  ."   -Q  Quiet mode (minimal output)" CR
  CR
  ." In Gforth:" CR
  ."   -s                    \ Enable stats" CR
  ."   S\" file.hvm\" RUN      \ Run file" CR
;

\ Test word
: TEST-CLI ( -- )
  ." CLI module loaded" CR
;
