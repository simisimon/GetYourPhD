import random, sys

out = []
def row(cells):
    out.append(''.join(cells))
def empty(n=1):
    for _ in range(n): row(['.'] * 5)
def d(s): out.append(s)

HEADER = """// Level 3 "The Camper Road Trip" - read top to bottom = driving order. One row = 5 units of road.
// Columns: 4 lanes (0 = left .. 3 = right) + 1 right shoulder (gas station / workshop).
// Legs with 3 lanes use columns 1-3 (column 0 is ignored).
// Directives: @leg DE|AT|IT   @sign L|R text   @finish
// Legend:
//   c slow car          f fast car from behind     T tailgating sports car   R Reviewer 2 (chases you)
//   v Vespa (weaves)    p parked car               = roadworks barrier       o Desk Reject pothole
//   k cow               r falling rock             B toll barrier (put one in every lane)
//   j jerry can (fuel)  a paper (+3)  i citation (+1)  s "Reviewer 2 approved" stamp (+6)  m Mate (+1, turbo)
//   P postcard (one per country)      G gas station (shoulder)   S workshop (shoulder)
// Rebuild with menu GetYourPhD/Build Level 3 (Camper Road Trip)."""

def put(r, col, ch):
    r[col] = ch

def leg(code, lanes, n_rows, seed, features):
    rnd = random.Random(seed)
    first = 4 - lanes
    cols = list(range(first, 4))
    d(f"@leg {code}")
    empty(3)
    i = 3
    gas_at = int(n_rows * 0.48)
    shop_at = int(n_rows * 0.72)
    postcard_at = features['postcard_at']
    special = dict(features.get('special', {}))
    jerries = set(int(n_rows * f) for f in (0.22, 0.62, 0.88))
    gas_done = shop_done = pc_done = False
    jer = sorted(jerries)
    while i < n_rows:
        r = ['.'] * 5
        if not gas_done and i >= gas_at - 4:
            d("@sign R GAS 500 m"); empty(4); i += 4
            r = ['.'] * 5; put(r, 4, 'G'); row(r); empty(4); i += 5; gas_done = True; continue
        if not shop_done and i >= shop_at - 4:
            d("@sign R WORKSHOP 500 m"); empty(4); i += 4
            r = ['.'] * 5; put(r, 4, 'S'); row(r); empty(4); i += 5; shop_done = True; continue
        ks = sorted(k for k in special if k <= i)
        if ks:
            n = special.pop(ks[0])(rnd, cols)
            i += n
            continue
        if not pc_done and i >= postcard_at:
            pc_done = True
            pc = cols[0] if rnd.random() < 0.5 else cols[-1]
            other = cols[1]
            put(r, other, 'c'); row(r); empty(1)
            r = ['.'] * 5; put(r, pc, 'P'); row(r); empty(2); i += 5; continue
        if jer and i >= jer[0]:
            jer.pop(0)
            put(r, rnd.choice(cols), 'j'); row(r); empty(1); i += 2; continue
        w = dict(features['weights'])
        q = features.get('quiet')
        warm = features.get('warmup', 0)
        if i < warm:
            # gentle start: only pickups while the player gets used to the camper
            w = {k: v for k, v in w.items() if k in ('pickups', 'empty')}
        elif q and q[0] <= i < q[1]:
            # no moving traffic right before the narrow old town: it would catch up with you inside it
            w = {k: v for k, v in w.items() if k in ('pickups', 'pothole', 'empty')}
        pat = rnd.choices(list(w.keys()), weights=list(w.values()))[0]
        n = PATTERNS[pat](rnd, cols)
        i += n
    return

# --- patterns: write rows, return how many rows were used ---
def p_pickups(rnd, cols):
    r = ['.'] * 5
    c = rnd.choice(cols)
    put(r, c, rnd.choice('aaiiim'))
    row(r)
    r = ['.'] * 5; put(r, c, 'i'); row(r)
    empty(1); return 3

def p_slowcar(rnd, cols):
    r = ['.'] * 5
    c = rnd.choice(cols); put(r, c, 'c')
    others = [x for x in cols if x != c]
    put(r, rnd.choice(others), rnd.choice('ai.'))
    row(r); empty(2); return 3

def p_twocars(rnd, cols):
    r = ['.'] * 5
    a = rnd.choice(cols[:-1]); put(r, a, 'c'); put(r, a + 1, 'c')
    row(r); empty(3); return 4

def p_fast(rnd, cols):
    r = ['.'] * 5; put(r, cols[0], 'f'); row(r)
    r = ['.'] * 5; put(r, cols[-1], 'a'); row(r); empty(1); return 3

def p_roadworks(rnd, cols):
    c = rnd.choice([cols[0], cols[-1]])
    d("@sign " + ("L" if c == cols[0] else "R") + " ROADWORKS")
    empty(2)
    for k in range(5):
        r = ['.'] * 5; put(r, c, '=')
        if k == 2: put(r, cols[1] if c == cols[-1] else cols[-2], 'i')
        row(r)
    empty(2); return 9

def p_pothole(rnd, cols):
    r = ['.'] * 5; c = rnd.choice(cols); put(r, c, 'o'); row(r); empty(2); return 3

def p_tailgater(rnd, cols):
    r = ['.'] * 5; put(r, cols[1], 'T'); row(r); empty(5); return 6

def p_reviewer2(rnd, cols):
    r = ['.'] * 5; put(r, cols[1], 'R'); row(r)
    empty(1)
    r = ['.'] * 5; put(r, rnd.choice(cols), 's'); row(r)
    empty(5); return 8

def p_cow(rnd, cols):
    r = ['.'] * 5; put(r, rnd.choice(cols), 'k'); row(r); empty(3); return 4

def p_rock(rnd, cols):
    r = ['.'] * 5; put(r, rnd.choice(cols), 'r'); row(r); empty(2); return 3

def p_rocks2(rnd, cols):
    a, b = rnd.sample(cols, 2)
    r = ['.'] * 5; put(r, a, 'r'); row(r); empty(1)
    r = ['.'] * 5; put(r, b, 'r'); row(r); empty(2); return 5

def p_toll(rnd, cols):
    d("@sign R TOLL - SLOW DOWN")
    empty(3)
    r = ['.'] * 5
    for c in cols: put(r, c, 'B')
    row(r); empty(3); return 7

def p_vespa(rnd, cols):
    r = ['.'] * 5; put(r, rnd.choice(cols), 'v'); row(r); empty(2); return 3

def p_vespas(rnd, cols):
    a, b = rnd.sample(cols, 2)
    r = ['.'] * 5; put(r, a, 'v'); row(r); empty(1)
    r = ['.'] * 5; put(r, b, 'v'); put(r, a, 'a'); row(r); empty(2); return 5

def p_parked(rnd, cols):
    n = rnd.randint(2, 3)
    for k in range(n):
        r = ['.'] * 5; put(r, cols[-1], 'p'); row(r)
    empty(2); return n + 2

def p_narrow(rnd, cols):
    # tight old-town street: parked cars on both sides, middle lane free
    d("@sign L CENTRO")
    for k in range(4):
        r = ['.'] * 5; put(r, cols[0], 'p'); put(r, cols[-1], 'p')
        if k == 1: put(r, cols[1], 'a')
        row(r)
    empty(2); return 6

def p_empty(rnd, cols):
    empty(2); return 2

PATTERNS = dict(pickups=p_pickups, slowcar=p_slowcar, twocars=p_twocars, fast=p_fast, roadworks=p_roadworks,
                pothole=p_pothole, tailgater=p_tailgater, reviewer2=p_reviewer2, cow=p_cow, rock=p_rock, rocks2=p_rocks2,
                toll=p_toll, vespa=p_vespa, vespas=p_vespas, parked=p_parked, narrow=p_narrow, empty=p_empty)

d(HEADER)
# Route: Italy -> Alps (Switzerland, then Austria) -> Germany, finishing at the Paulinum in Leipzig.
leg('IT', 3, 100, 33, dict(
    postcard_at=64,
    weights=dict(pickups=5, vespa=4, vespas=2, parked=3, slowcar=3, pothole=2, empty=2),
    special={48: p_reviewer2, 90: p_narrow},
    warmup=16,
    quiet=(72, 100),
))
leg('AT', 3, 112, 22, dict(
    postcard_at=76,
    weights=dict(pickups=5, slowcar=3, cow=3, rock=3, rocks2=1, pothole=2, empty=3),
    special={24: p_toll, 56: lambda r, c: (d("@sign L ÖSTERREICH"), empty(1), 1)[2], 86: p_reviewer2},
))
leg('DE', 4, 112, 11, dict(
    postcard_at=80,
    weights=dict(pickups=5, slowcar=6, twocars=2, fast=3, roadworks=2, pothole=2, empty=3),
    special={14: p_tailgater, 56: p_tailgater, 90: p_reviewer2,
             104: lambda r, c: (d("@sign R LEIPZIG"), empty(1), 1)[2]},
    quiet=(101, 112),
))
empty(2)
r = ['.'] * 5; r[2] = 'a'; row(r)
empty(3)
d("@finish")
empty(4)

open(sys.argv[1], 'w', encoding='utf-8', newline='\n').write('\n'.join(out) + '\n')
rows = [l for l in out if l and not l.startswith('@') and not l.startswith('//')]
print('rows', len(rows))
from collections import Counter
print(Counter(ch for l in rows for ch in l if ch != '.'))
