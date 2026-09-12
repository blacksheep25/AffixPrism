# Similarity benchmark — beta 0.4.0

Eight labelled synthetic cases passed on 9 September 2026:

| Peer relative to the bow fixture | Expected valuation eligibility | Result |
| --- | --- | --- |
| Identical item | Include | Pass |
| Nearby physical modifier roll | Include | Pass |
| Missing additional arrow | Exclude | Pass |
| Lower attack-skill level | Exclude | Pass |
| Much lower physical damage | Exclude | Pass |
| Different corruption state | Exclude | Pass |
| Different weapon base, same class and performance | Include | Pass |
| Explicit arrow instead of rune arrow | Exclude | Pass |

Additional checks verify duplicate sellers do not increase independent-seller coverage and mixed-currency medians agree only when valid conversion rates exist. Missing rates suppress insufficient-seller recommendations.

The fixture is transcribed from a reported bow; peer transformations and expected labels are authored regression cases, not observed sale data. Eight passing cases are **not** an accuracy percentage for the market. A consented, sanitised dataset of real comparables and independently assessed labels is still needed to assess valuation quality. Completed-sale values are not available through this adapter.
