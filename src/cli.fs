\ cli.fs - Command-line interface

\ Flags
VARIABLE COMPILED?
VARIABLE STATS?
VARIABLE QUIET?

0 COMPILED? !
0 STATS? !
0 QUIET? !

\ Parse arguments (stub)
: PARSE-ARGS ( -- )
  \ TODO: parse command-line arguments
;

\ Run mode
: RUN-FILE ( filename -- )
  \ TODO: load file, run main, print result
  DROP
  ." (running file...)" CR
;

\ Statistics display
: PRINT-STATS ( -- )
  STATS? @ IF
    ." WORK: " ITR-COUNT @ . ." interactions" CR
    \ TODO: calculate MIPS
  THEN
;

\ Main entry point
: MAIN ( -- )
  PARSE-ARGS
  \ TODO: dispatch to run/serve mode
  ." ForthVM CLI ready" CR
;

\ Help text
: .HELP ( -- )
  ." Usage: fvm run <file.hvm> [-C] [-s] [-Q]" CR
  ."   -C  Compiled mode (not yet implemented)" CR
  ."   -s  Show statistics" CR
  ."   -Q  Quiet mode (minimal output)" CR
;

\ Test word
: TEST-CLI ( -- )
  ." CLI module loaded" CR
;
