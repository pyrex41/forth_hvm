\ core.fs - Core primitives: tags, constants, term packing

\ IC Term Tags (from IC.md)
0 CONSTANT TAG-LAM
1 CONSTANT TAG-APP
2 CONSTANT TAG-SUP
3 CONSTANT TAG-DUP
4 CONSTANT TAG-VAR
5 CONSTANT TAG-ERA
6 CONSTANT TAG-CTR
7 CONSTANT TAG-U32
8 CONSTANT TAG-OP2
9 CONSTANT TAG-REF
10 CONSTANT TAG-MATCH

\ Term packing/unpacking (adapt to CELL size)
\ Layout for 64-bit: tag:5b lab:18b val:41b (5+18+41=64)
\ tag: bits 0-4   (mask $1F, shift 0)
\ lab: bits 5-22  (mask $3FFFF, shift 5)
\ val: bits 23-63 (mask $1FFFFFFFFFF, shift 23)

\ Bit masks and shifts
$1F CONSTANT TAG-MASK     \ 5 bits: 31
$3FFFF CONSTANT LAB-MASK  \ 18 bits: 262143
$1FFFFFFFFFF CONSTANT VAL-MASK  \ 41 bits: 2199023255551

0 CONSTANT TAG-SHIFT
5 CONSTANT LAB-SHIFT
23 CONSTANT VAL-SHIFT

: PACK-TERM ( tag lab val -- cell )
  \ Pack: val<<23 | lab<<5 | tag
  \ Stack: tag lab val
  VAL-SHIFT LSHIFT           \ tag lab (val<<23)
  SWAP LAB-SHIFT LSHIFT OR   \ tag ((val<<23)|(lab<<5))
  SWAP OR                     \ ((val<<23)|(lab<<5)|tag)
;

: UNPACK-TERM ( cell -- tag lab val )
  DUP TAG-MASK AND                        \ cell tag
  OVER LAB-SHIFT RSHIFT LAB-MASK AND      \ cell tag lab
  ROT VAL-SHIFT RSHIFT VAL-MASK AND       \ tag lab val
;

\ Utility accessors
: GET-TAG ( cell -- tag )
  TAG-MASK AND
;

: GET-LAB ( cell -- lab )
  LAB-SHIFT RSHIFT LAB-MASK AND
;

: GET-VAL ( cell -- val )
  VAL-SHIFT RSHIFT VAL-MASK AND
;

\ Helper word: subtract one cell size from address
: CELL- ( addr -- addr-CELL )
  [ 1 CELLS ] LITERAL -
;

\ Test pack/unpack roundtrip
: TEST-PACK-UNPACK ( -- )
  ." Testing pack/unpack roundtrip..." CR

  \ Test 1: Simple values
  TAG-LAM 0 0 PACK-TERM UNPACK-TERM
  ." Test 1: tag=" . ." lab=" . ." val=" . CR
  TAG-LAM = SWAP 0 = AND SWAP 0 = AND
  IF ." Test 1: PASS" CR ELSE ." Test 1: FAIL" CR THEN

  \ Test 2: Max tag value (31)
  31 12345 67890 PACK-TERM UNPACK-TERM
  \ Stack now: tag lab val (top)
  ." Test 2: val=" DUP . ." lab=" OVER . ." tag=" 2 PICK . CR
  \ Check: val=67890, lab=12345, tag=31
  67890 = >R                    \ Check val, save flag
  12345 = R> AND >R            \ Check lab, AND with saved, save
  31 = R> AND                   \ Check tag, AND with saved
  IF ." Test 2: PASS" CR ELSE ." Test 2: FAIL" CR THEN

  \ Test 3: Large values
  TAG-APP 262143 1099511627775 PACK-TERM UNPACK-TERM  \ max lab (2^18-1), max val (2^40-1)
  \ Stack: tag lab val
  ." Test 3: val=" DUP . ." lab=" OVER . ." tag=" 2 PICK . CR
  \ Check: val=1099511627775, lab=262143, tag=TAG-APP(1)
  1099511627775 = >R
  262143 = R> AND >R
  TAG-APP = R> AND
  IF ." Test 3: PASS" CR ELSE ." Test 3: FAIL" CR THEN
;

\ Test word
: TEST-CORE ( -- )
  ." Core module loaded" CR
  ." Tags defined: LAM=" TAG-LAM .
  ." APP=" TAG-APP .
  ." SUP=" TAG-SUP . CR
  TEST-PACK-UNPACK
;
