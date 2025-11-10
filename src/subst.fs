\ subst.fs - Substitution map for variable binding

\ Substitution table size (1024 entries)
1024 CONSTANT SUBST-SIZE

\ Substitution table: each entry is (name-hash loc use-count)
\ We'll use 3 cells per entry: hash (key), location, use-count
CREATE SUBST-TABLE SUBST-SIZE 3 * CELLS ALLOT

\ Current substitution count
VARIABLE SUBST-COUNT
0 SUBST-COUNT !

\ Simple hash function for strings
: HASH-STRING ( c-addr u -- hash )
  0 SWAP 0 ?DO           \ hash addr
    OVER C@ +            \ Add character
    31 *                 \ Multiply by 31
  1 +LOOP
  NIP
  SUBST-SIZE MOD         \ Modulo table size
;

\ Clear substitution table
: SUBST-CLEAR ( -- )
  SUBST-TABLE SUBST-SIZE 3 * CELLS ERASE
  0 SUBST-COUNT !
;

\ Find entry in substitution table (linear probe on collision)
: SUBST-FIND ( c-addr u -- entry-addr | 0 )
  2DUP HASH-STRING       \ addr u hash
  CELLS 3 * SUBST-TABLE + \ addr u entry-addr
  >R 2DUP R@             \ addr u addr u entry
  @ 0= IF                \ Empty slot found
    2DROP R> DROP 0 EXIT
  THEN
  \ TODO: compare stored string with input
  \ For now, just use hash comparison (collision-prone but simple)
  HASH-STRING R@ @ = IF
    2DROP R> EXIT        \ Found matching hash
  ELSE
    R> DROP 0            \ Hash mismatch (should probe next)
  THEN
;

\ Store substitution (name -> location)
: SUBST-PUT ( c-addr u loc -- )
  -ROT                   \ loc addr u
  2DUP HASH-STRING       \ loc addr u hash
  CELLS 3 * SUBST-TABLE + \ loc addr u entry-addr
  >R                     \ loc addr u | R: entry-addr
  HASH-STRING            \ loc hash | R: entry-addr
  R@ !                   \ Store hash at entry | R: entry-addr
  R> CELL+ !             \ Store location at entry+CELL
  SUBST-COUNT @ 1+ SUBST-COUNT !
;

\ Get substitution (name -> location | 0 if not found)
: SUBST-GET ( c-addr u -- loc | 0 )
  SUBST-FIND DUP 0= IF EXIT THEN
  CELL+ @                \ Get location from entry
;

\ Track variable use (for affine checking)
: SUBST-USE ( c-addr u -- )
  SUBST-FIND DUP 0= IF
    DROP
    S" Variable not bound" AFFINE-ERROR
  THEN

  2 CELLS + DUP @        \ Get use count
  DUP 0> IF
    DROP
    S" Affine variable used more than once" AFFINE-ERROR
  THEN
  1+ SWAP !              \ Increment use count
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
    ." FAIL" CR
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
    ." FAIL" CR
  THEN

  \ Test 4: SUBST-CLEAR resets
  ." Test 4: Clear resets... "
  SUBST-CLEAR
  S" x" SUBST-GET 0= IF
    ." PASS" CR
  ELSE
    ." FAIL" CR
  THEN

  SUBST-CLEAR
;

\ Test word
: TEST-SUBST ( -- )
  ." Substitution module loaded" CR
  ." Table size: " SUBST-SIZE . ." entries" CR
  TEST-SUBST-OPS
;
