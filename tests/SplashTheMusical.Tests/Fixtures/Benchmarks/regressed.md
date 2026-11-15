# STM benchmark results

A run of the comparison test. JIT parse is 11% slower, JIT render allocates 11% more, AOT parse is 10% slower and
allocates 10% more, which is within the budget, and AOT render allocates where the committed run did not.

| Property | Value |
|---|---|
| Machine | fixture |

## Results

| Job | Case | Stage | Chars | ns | Bytes | ns/char | Bytes/char |
|---|---|---|---:|---:|---:|---:|---:|
| JIT | pie-small | parse | 100 | 1110.0 | 2000 | 11.10 | 20.00 |
| JIT | pie-small | render | 100 | 5000.0 | 8880 | 50.00 | 88.80 |
| AOT | pie-small | parse | 100 | 990.0 | 2200 | 9.90 | 22.00 |
| AOT | pie-small | render | 100 | 4500.0 | 24 | 45.00 | 0.24 |
