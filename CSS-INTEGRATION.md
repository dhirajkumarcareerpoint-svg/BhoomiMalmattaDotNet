# CSS INTEGRATION

* `Content/css/site.css` is the supplied stylesheet (palette `--p:#6b2a7a`, `--p2:#3c1053`, `--a:#ac5288`, login layout, wizard steps, cards, tiles, sidebar, header, dashboard grid).
* **Only two corrections were made:**
  1. `inset:0` (in `.scrim.open`) became `top:0;right:0;bottom:0;left:0` because `inset` is unsupported by older browsers still common in municipal offices.
  2. The `body` font stack gained `"Nirmala UI","Noto Sans Devanagari",Mangal` before `Roboto` so Marathi renders correctly on Windows.
* Extra rules the pages need (tables, badges, notices, pager, timeline, key/value lists, validation text, print rules) are in a **separate** `Content/css/app.css`, written in the same palette; `site.css` itself is otherwise untouched. Bootstrap is not used.
* Both files are linked from `Site.Master` (Citizen/Department layout, one header, role-based navigation) and `Auth.Master` (login layout).
* Marathi: all source files are UTF-8 (BOM), `Web.config` sets UTF-8 request/response encoding, pages declare `<meta charset="utf-8">`. Devanagari in PDFs uses the bundled Noto Sans Devanagari (SIL OFL) font.
