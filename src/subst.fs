\ subst.fs - Substitution map for variable binding

\ Simple string comparison (returns 0 if equal)
: STR= ( c-addr1 u1 c-addr2 u2 -- flag )
  ROT OVER <> IF DROP 2DROP 0 EXIT THEN  \ Lengths differ
  0 ?DO
    OVER I + C@ OVER I + C@ <> IF DROP 2DROP 0 EXIT THEN
  LOOP
  DROP 2DROP -1
;

\ Substitution table: simple array of entries
\ Each entry is 3 cells: name-addr, name-len, location
\ Use linear search for simplicity
CREATE SUBST-TABLE 4 3 * CELLS ALLOT

\ Current substitution count
VARIABLE SUBST-COUNT
0 SUBST-COUNT !

\ Clear substitution table
: SUBST-CLEAR ( -- )
  SUBST-TABLE 4 3 * CELLS ERASE
  0 SUBST-COUNT !
;

\ Find entry in substitution table (linear search)
: SUBST-FIND ( c-addr u -- entry-addr | 0 )
  SUBST-COUNT @ 0 ?DO
    \ Calculate entry address
    I 3 * CELLS SUBST-TABLE + ( c-addr u entry-addr )
    \ Check if name matches
    DUP @ ( c-addr u entry-addr stored-addr )
    OVER 2 PICK ( c-addr u entry-addr stored-addr c-addr u )
    DUP CELL+ @ ( c-addr u entry-addr stored-addr c-addr u stored-len )
    STR= IF
      \ Strings match
      NIP NIP NIP EXIT
    THEN
    DROP
  LOOP
  0  \ Not found
;

\ Store substitution (name -> location)
: SUBST-PUT ( c-addr u loc -- )
  SUBST-COUNT @ 4 >= IF
    DROP 2DROP
    S" Substitution table full" PARSE-ERROR
    EXIT
  THEN
   \ Allocate permanent storage for the name string
   DUP ALLOC ( c-addr u loc name-addr )
  >R                     \ c-addr u loc | R: name-addr
   \ Copy the string manually
   DUP >R ( c-addr u loc | R: name-addr u )
   0 ?DO
     2 PICK I + C@ R@ I + C!
   LOOP
   R> DROP ( c-addr u loc | R: name-addr )
  \ Find next free entry
  SUBST-COUNT @ 3 * CELLS SUBST-TABLE + ( c-addr u loc entry-addr | R: name-addr )
  >R                     \ c-addr u loc | R: name-addr entry-addr
  \ Store name-addr
  R@ !                   \ c-addr u | R: name-addr entry-addr
  \ Store name-len
  R@ CELL+ !             \ c-addr | R: name-addr entry-addr
  \ Store location
  R> 2 CELLS + !         \ | R: name-addr
  RDROP                  \ |
  SUBST-COUNT @ 1+ SUBST-COUNT !
;

\ Get substitution (name -> location | 0 if not found)
: SUBST-GET ( c-addr u -- loc | 0 )
  SUBST-FIND DUP 0= IF EXIT THEN
  2 CELLS + @            \ Get location from entry
;

\ Track variable use (for affine checking) - DISABLED
: SUBST-USE ( c-addr u -- )
  \ For now, just check if variable exists
  SUBST-GET 0= IF
    S" Variable not bound" AFFINE-ERROR
  THEN
;

\ Test substitution map
: TEST-SUBST-OPS ( -- )
  ." Testing substitution operations..." CR

  SUBST-CLEAR

  \ Test 1: Store and retrieve
  ." Test 1: Store and retrieve... "
  S" x" 100 SUBST-PUT
  S" x" SUBST-GET 100 = IF
    ." PASS" CR
  ELSE
    ." FAIL - got: " S" x" SUBST-GET . CR
  THEN

  \ Test 2: Multiple bindings
  ." Test 2: Multiple bindings... "
  S" y" 200 SUBST-PUT
  S" z" 300 SUBST-PUT
  S" x" SUBST-GET 100 =
  S" y" SUBST-GET 200 = AND
  S" z" SUBST-GET 300 = AND IF
    ." PASS" CR
  ELSE
    ." FAIL" CR
  THEN

  \ Test 3: Unbound variable returns 0
  ." Test 3: Unbound variable... "
  S" unbound" SUBST-GET 0= IF
    ." PASS" CR
  ELSE
    ." FAIL - got: " S" unbound" SUBST-GET . CR
  THEN

  \ Test 4: SUBST-CLEAR resets
  ." Test 4: Clear resets... "
  SUBST-CLEAR
  S" x" SUBST-GET 0= IF
    ." PASS" CR
  ELSE
    ." FAIL - got: " S" x" SUBST-GET . CR
  THEN

  SUBST-CLEAR
;

\ Test word
: TEST-SUBST ( -- )
  ." Substitution module loaded" CR
  ." Table size: 4 entries" CR
  TEST-SUBST-OPS
;
