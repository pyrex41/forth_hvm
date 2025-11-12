\ errors.fs - Error handling and debugging infrastructure

\ Debug flag
VARIABLE DEBUG?
0 DEBUG? !

\ Position tracking for error reporting
VARIABLE CURRENT-LINE
VARIABLE CURRENT-COL
1 CURRENT-LINE !
1 CURRENT-COL !

\ Error types
 : PARSE-ERROR ( addr len -- )
   CR ." Parse Error: " TYPE CR
   ." at line " CURRENT-LINE @ . ." , column " CURRENT-COL @ . CR
   ABORT
 ;

 : RUNTIME-ERROR ( addr len -- )
   CR ." Runtime Error: " TYPE CR
   ABORT
 ;

 : AFFINE-ERROR ( addr len -- )
   CR ." Affine Variable Error: " TYPE CR
   ABORT
 ;

 : FILE-ERROR ( addr len -- )
   CR ." File Error: " TYPE CR
   ABORT
 ;

\ Debug/trace utilities
: TRACE ( addr len -- )
  DEBUG? @ IF
    ." [TRACE] " TYPE CR
  ELSE
    2DROP
  THEN
;

: ASSERT ( flag addr len -- )
  ROT 0= IF
    ." Assertion failed: " TYPE CR
    ABORT
  ELSE
    2DROP
  THEN
;

\ Test word
: TEST-ERRORS ( -- )
  ." Errors module loaded" CR
;
