# Fixture provenance

`quarterstaff.txt` and `magic-crimson-amulet.txt` preserve previously reported item examples without account details. `desecrated-bow.txt` and `lineage-gem.txt` are text transcriptions of user-provided screenshots, with explicit metadata markers for the visible types; they are not raw clipboard captures. `currency-stack.txt` and `socket-source.json` are synthetic protocol fixtures. Socket artwork URLs use non-existent fixture paths and are never fetched by tests.

`similarity-benchmark.json` is a hand-labelled *synthetic regression benchmark*, not market ground truth: transformations test that nearby rolls qualify and incompatible peers do not. It cannot establish actual resale-price accuracy. Cases and thresholds are reviewed in source; expand with consented, sanitised real datasets before making valuation claims.
