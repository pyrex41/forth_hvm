\ parse.fs - Parser for IC grammar

\ Tokenizer stub
: NEXT-TOKEN ( -- addr len )
  \ TODO: implement tokenizer
  0 0
;

\ Parser stubs (minimal subset first: LAM, APP, VAR)
: PARSE-LAM ( -- term )
  \ TODO: parse λx.body
  0
;

: PARSE-APP ( -- term )
  \ TODO: parse (@f x)
  0
;

: PARSE-VAR ( -- term )
  \ TODO: parse variable reference
  0
;

: PARSE-TERM ( -- term )
  \ TODO: dispatcher for all term types
  0
;

\ Test word
: TEST-PARSE ( -- )
  ." Parse module loaded" CR
;
