\ book.fs - Book loading and function dictionary

\ Function dictionary (256 entries)
CREATE BOOK-DICT 256 CELLS ALLOT

\ Store function in book
: BOOK-PUT ( name arity term -- )
  \ TODO: store in dictionary
  DROP DROP DROP
;

\ Find function in book
: BOOK-FIND ( name -- arity term | 0 0 )
  \ TODO: lookup in dictionary
  DROP 0 0
;

\ Two-pass loading
: PARSE-DEF ( -- )
  \ TODO: parse 'name = term'
;

: LINK-REFS ( -- )
  \ TODO: resolve '@name' references
;

\ Main entry point
: LOAD-BOOK ( filename-addr len -- )
  \ TODO: open file, parse definitions, link
  2DROP
;

\ Test word
: TEST-BOOK ( -- )
  ." Book module loaded" CR
;
