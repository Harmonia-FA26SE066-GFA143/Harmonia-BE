"""Fill the FPTU Report5 Unit Test Google Sheet from test/Harmonia.Application.Tests.

Usage:
    python -P build_functions_sheet.py --repo <solution root> --sheet <Google Sheet link> [--key <sa.json>]
                                       [--trx <results.trx>] [--only F1,F8-04] [--dry-run]

- Sheet 'Functions': one row per method under test. Every [Fact]/[Theory] is assigned to the method it calls on
  `_sut` (or `sut`); validators become `<Validator>.Validate`. Requirement Name comes from requirement_map.json.
- With --trx: one tab per function (named after the function, Class.Method when the name repeats), built from the template tab 'Function 1', or
  from an existing function tab when the template is gone. One UTCID column per executed test case of the trx;
  Condition = scenario from the test name + the theory's parameters, Confirm = expected outcome / expected
  parameters / asserted exception, Passed/Failed and Executed Date from the trx. The sample tabs
  'Function 1/2/3' are then deleted and the 'Statistics' sheet is rebuilt over every function tab.
- --only limits which tabs are (re)built, by Function Code prefix; the Functions sheet is always rewritten in full.
- --dry-run prints the rows and stops without touching the Sheet. Writing (sheets_writer.py) needs
  google-api-python-client + google-auth and a service account key that has Editor access to the Sheet.
Exit code 2 = a test class has no group in requirement_map.json, or a test could not be mapped to a method.
"""
import argparse
import collections
import datetime
import glob
import html
import json
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

HERE = os.path.dirname(os.path.abspath(__file__))
FIRST_ROW = 11  # first data row of the template's Functions table (1-based)
SAMPLE_TABS = ('Function 1', 'Function 2', 'Function3')
ATTR = re.compile(r'^\s*\[(Fact|Theory)\b', re.M)
PURE = ('PagingRequest', 'PagedList', 'SystemSettingKeys', 'LiturgicalSeasonResolver')
PRECONDITION = 'Dependencies mocked, no DB/server'
EXPECTED_PARAM = re.compile(r'^(expected\w*|code|\w+Code)$')
ABNORMAL = re.compile(r'not found|invalid|forbidden|denied|fails?\b|error|not allowed|too (large|long|soon|many|short)'
                      r'|already|required|missing|inactive|unknown|bad |cannot|is rejected|returns? code|out of'
                      r'|not (editable|lead|active|submitted|latest|approved|published)|unreadable|unavailable|conflict')
BOUNDARY = re.compile(r'exactly|boundary|limit|\bmax|\bmin|edge|zero|empty|at the cap|cutoff|first attempt|\blast\b')


# ---------- scanning ----------

CANCEL = re.compile(r'^(_?ct|cancellationToken|CancellationToken\.None|default|TestContext\.Current\.CancellationToken)$')
PAIRS = {'(': ')', '[': ']', '{': '}'}
BACKSLASH, QUOTES = chr(92), '"' + chr(39)


def close_of(s, i):
    """Index of the bracket closing s[i], skipping string and char literals."""
    depth, j, q = 0, i, None
    while j < len(s):
        ch = s[j]
        if q:
            if ch == BACKSLASH:
                j += 1
            elif ch == q:
                q = None
        elif ch in QUOTES:
            q = ch
        elif ch in PAIRS:
            depth += 1
        elif ch in ')]}':
            depth -= 1
            if depth == 0:
                return j
        j += 1
    return len(s)


def split_top(s):
    """Split on commas outside brackets and strings."""
    out, start, j = [], 0, 0
    while j < len(s):
        ch = s[j]
        if ch in PAIRS:
            j = close_of(s, j)
        elif ch in QUOTES:
            k = j + 1
            while k < len(s) and s[k] != ch:
                k += 2 if s[k] == BACKSLASH else 1
            j = k
        elif ch == ',':
            out.append(s[start:j])
            start = j + 1
        j += 1
    out.append(s[start:])
    return [x.strip() for x in out if x.strip()]


def take_expr(s, i):
    """Expression starting at s[i], up to the first ';' outside brackets."""
    j = i
    while j < len(s) and s[j] != ';':
        j = close_of(s, j) + 1 if s[j] in PAIRS else j + 1
    return s[i:j].strip()


def args_at(s, i):
    """Top-level arguments of the call whose '(' is at s[i]."""
    return split_top(s[i + 1:close_of(s, i)])


def param_names(src, meth):
    m = re.search(r'\bpublic\b[^;{=()]*?\b' + re.escape(meth) + r'\s*\(', src)  # the declaration, not a call
    if not m:
        return []
    names = [re.findall(r'\w+', p.split('=')[0])[-1] for p in args_at(src, m.end() - 1)]
    return [n for n in names if n != 'cancellationToken']


def resolve(expr, body, src, depth=0):
    """Follow a local variable or a parameterless helper of the test class back to its expression."""
    e = expr.strip()
    if depth > 2:
        return e
    if re.fullmatch(r'[A-Za-z_]\w*', e):
        m = re.search(r'(?:\bvar|[\w<>\[\]?]+)\s+' + e + r'\s*=\s*(?!=)', body)
        if m:
            return resolve(take_expr(body, m.end()), body, src, depth + 1)
    m = re.fullmatch(r'([A-Za-z_]\w*)\(\)', e)
    if m:
        h = re.search(r'\b' + m.group(1) + r'\s*\([^)]*\)\s*(=>|\{\s*return\b)', src)
        if h:
            return resolve(take_expr(src, h.end()), body, src, depth + 1)
    return e


def pretty(v):
    v = re.sub(r'\s+', ' ', v).strip()
    v = re.sub(r'(?<![\w.])_(?=[a-z])', '', v)  # _event.Id -> event.Id
    return v if len(v) <= 50 else v[:47] + '...'


def expand(name, expr, body, src):
    """(input name, value) pairs; an object initializer becomes one input per property."""
    e = resolve(expr, body, src)
    m = re.match(r'new\s*[\w.<>]*\s*(?:\(\s*\))?\s*\{', e)  # also target-typed new() { ... }
    if m:
        inner = e[m.end() - 1:]
        fields = split_top(inner[1:close_of(inner, 0)])
        pairs = [tuple(x.strip() for x in f.split('=', 1)) for f in fields if '=' in f]
        return [(k, pretty(resolve(v, body, src))) for k, v in pairs] or [(name, 'empty ' + e.split('{')[0][4:].strip())]
    if re.fullmatch(r'new\s+[\w.<>]+\s*\(\s*\)', e):
        return [(name, 'empty ' + e[4:].split('(')[0].strip())]
    return [(name, pretty(e))]


def call_inputs(body, src, service_src, cls, meth):
    """Inputs of the call under test in one test body: [(input name, value or theory parameter)]."""
    if meth == 'Validate':
        # ErrorCodesOf(new XValidator(), request) or validator.Validate(request)
        m = re.search(r'ErrorCodesOf\(', body)
        args = args_at(body, m.end() - 1)[1:] if m else []
        if not args:
            m = re.search(r'\.(?:Test)?Validate\(', body)
            args = args_at(body, m.end() - 1) if m else []
        return [x for a in args[:1] for x in expand('request', a, body, src)]
    if meth == 'ctor':
        m = re.search(r'new\s+' + re.escape(cls) + r'\b[^;{(]*([({])', body)
        if not m:
            return []
        if m.group(1) == '{':
            return expand(cls, body[m.start():], body, src)
        return [(a if re.fullmatch(r'\w+', a) else f'arg{k + 1}', pretty(a))
                for k, a in enumerate(args_at(body, m.end() - 1))]
    m = re.search(r'\b_?sut\.' + re.escape(meth) + r'\s*(?:<[^>]*>)?\s*\(', body)
    if not m:
        return helper_inputs(body, src, service_src, cls, meth)
    args = [a for a in args_at(body, m.end() - 1) if not CANCEL.match(a)]
    names = param_names(service_src, meth)
    out = []
    for k, a in enumerate(args):
        named = re.match(r'(\w+)\s*:\s*(?!:)', a)  # named argument: includeDraft: true
        if named:
            name, a = named.group(1), a[named.end():]
        else:
            name = names[k] if k < len(names) else (a if re.fullmatch(r'\w+', a) else f'arg{k + 1}')
        out += expand(name, a, body, src)
    return out


def helper_inputs(body, src, service_src, cls, meth):
    """The test calls a helper of its class that calls the service (SubmitAsync(assignment, NewAudio())):
    take the helper's service call, with the helper's parameters replaced by the test's arguments."""
    for h in re.finditer(r'\b(?:private|internal|public)\b[^;{=]*?\b(\w+)\s*\(([^()]*)\)\s*(=>|\{)', src):
        hb = take_expr(src, h.end()) if h.group(3) == '=>' else src[h.end() - 1:close_of(src, h.end() - 1) + 1]
        if not re.search(r'\b_?sut\.' + re.escape(meth) + r'\s*[(<]', hb):
            continue
        call = re.search(r'(?<![\w.])' + re.escape(h.group(1)) + r'\s*\(', body)
        if not call:
            continue
        params = [re.findall(r'\w+', p.split('=')[0])[-1] for p in split_top(h.group(2))]
        given = args_at(body, call.end() - 1)
        sub = {p: given[k] for k, p in enumerate(params) if k < len(given)}
        out = []
        for name, value in call_inputs(hb, src, service_src, cls, meth):
            head, dot, rest = value.partition('.')
            if head in sub:
                value = pretty(resolve(sub[head], body, src)) + dot + rest
            out.append((name, value))
        return out
    return []


def service_methods(repo, cls):
    path = os.path.join(repo, 'src', 'Harmonia.Application', 'Services', cls + '.cs')
    if not os.path.exists(path):
        return set()
    src = open(path, encoding='utf-8').read()
    return set(re.findall(r'public\s+(?:static\s+)?(?:async\s+)?[\w<>\[\],?.() ]+?\s(\w+)\s*\(', src))


def scan_tests(repo):
    """Groups test methods by (file, class under test, method under test)."""
    groups = collections.OrderedDict()
    unresolved, mocks, test_info = [], {}, {}
    for path in sorted(glob.glob(os.path.join(repo, 'test', 'Harmonia.Application.Tests', '*Tests.cs'))):
        fn = os.path.basename(path)
        src = open(path, encoding='utf-8').read()
        mocks[fn] = sorted(set(re.findall(r'Substitute\.For<(I\w+)>', src)))
        m = re.search(r'_sut\s*=\s*new\s+(\w+)', src) or re.search(r'(\w+)\s+_sut\b', src)
        sut = m.group(1) if m else None
        methods = service_methods(repo, sut) if sut else set()
        starts = [a.start() for a in ATTR.finditer(src)] + [len(src)]
        for a, b in zip(starts, starts[1:]):
            blk = src[a:b]
            nm = re.search(r'public\s+(?:async\s+)?\S+\s+(\w+)\s*\(', blk)
            if not nm:
                continue
            name, head, body = nm.group(1), blk[:nm.start()], blk[nm.end():]
            cases = max(1, len(re.findall(r'\[InlineData', head)))
            pre = name.split('_')[0]
            cls = meth = None
            if fn == 'ValidatorTests.cs':
                v = re.search(r'new (\w+Validator)\(', blk)
                cls, meth = (v.group(1) if v else 'Search*RequestValidator (all search requests)'), 'Validate'
            elif fn == 'PagingTests.cs':
                cls, meth = pre, 'ctor'
            elif fn == 'LiturgicalSeasonResolverTests.cs':
                cls, meth = 'LiturgicalSeasonResolver', pre
            else:
                cls = sut
                call = re.search(r'\b_?sut\.(\w+)\s*[(<]', body)
                guess = pre if pre.endswith('Async') else pre + 'Async'
                if call and call.group(1) in methods:
                    meth = call.group(1)
                elif guess in methods:
                    meth = guess
                else:
                    # Test goes through a local helper: take the first service method name in the body.
                    hits = [x for x in re.findall(r'(?<![\w.])(?:_?sut\.)?(\w+Async)\s*\(', body) if x in methods]
                    meth = hits[0] if hits else None
                if not meth and pre == 'SeasonValidator':
                    cls, meth = 'SaveLiturgicalSeasonRequestValidator', 'Validate'
                if not meth and pre == 'Definitions':
                    cls, meth = 'SystemSettingKeys', 'Definitions'
                if not meth:
                    unresolved.append(f'{fn}: {name}')
                    meth = '?' + pre
            key = (fn, cls, meth)
            g = groups.setdefault(key, {'tests': [], 'cases': 0})
            g['tests'].append(name)
            g['cases'] += cases
            exc = re.findall(r'Throws(?:Any)?(?:Async)?<(\w+)>', body)
            svc_path = os.path.join(repo, 'src', 'Harmonia.Application', 'Services', f'{cls}.cs')
            svc = open(svc_path, encoding='utf-8').read() if os.path.exists(svc_path) else ''
            test_info[(fn[:-3], name)] = {'key': key, 'order': len(test_info), 'exceptions': exc,
                                          'inputs': call_inputs(body, src, svc, cls, meth) if meth else []}
    return groups, mocks, unresolved, test_info


def split_args(s):
    return re.split(r', (?=[A-Za-z_]\w*: )', s) if s else []


def read_trx(path):
    """{(TestClass, method): [{'args': [(name, value)], 'outcome', 'date'}]} in execution order."""
    out = collections.defaultdict(list)
    for el in ET.parse(path).getroot().iter():
        if not el.tag.endswith('UnitTestResult'):
            continue
        full = html.unescape(el.get('testName'))
        base, _, args = full.partition('(')
        cls, meth = base.rsplit('.', 2)[-2:]
        pairs = [tuple(p.split(': ', 1)) if ': ' in p else ('arg', p) for p in split_args(args.rstrip(')'))]
        start = el.get('startTime')
        date = None
        if start:
            # Executed date in Vietnam time, as the rest of the project.
            t = datetime.datetime.fromisoformat(start[:26] + start[-6:] if '.' in start else start)
            date = (t.astimezone(datetime.timezone(datetime.timedelta(hours=7)))).date()
        out[(cls, meth)].append({'args': pairs, 'outcome': el.get('outcome'), 'date': date})
    return out


def method_loc(repo, cls, meth):
    """Non-blank lines of the method body (or of the whole class for validators / pure types)."""
    if '*' in cls:  # the shared search-validator row covers several classes
        return None
    roots = [os.path.join(repo, 'src', p) for p in ('Harmonia.Application', 'Harmonia.Domain')]
    files = [f for r in roots for f in glob.glob(os.path.join(r, '**', cls.split(' ')[0] + '.cs'), recursive=True)]
    if not files:
        return None
    src = open(files[0], encoding='utf-8').read()
    if meth in ('Validate', 'ctor') or cls in ('SystemSettingKeys',):
        return sum(1 for line in src.splitlines() if line.strip())
    m = re.search(r'\b(?:public|internal|private)\b[^;{=]*?\b' + re.escape(meth) + r'\s*[(<]', src)
    if not m:
        return None
    i = src.find('{', m.end())
    arrow = src.find('=>', m.end())
    if arrow != -1 and (i == -1 or arrow < i):
        end = src.find(';', arrow)
        return src[m.start():end].count('\n') + 1
    depth, j = 0, i
    while j < len(src):
        depth += {'{': 1, '}': -1}.get(src[j], 0)
        if depth == 0:
            break
        j += 1
    return sum(1 for line in src[m.start():j + 1].splitlines() if line.strip())


# ---------- rows ----------

def words(s):
    return re.sub(r'(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])', ' ', s).lower()


def name_parts(test_name):
    parts = [p for p in test_name.split('_') if p and p != 'Async']
    return parts[1:] if len(parts) > 1 else parts


def scenario(test_name):
    return ' → '.join(words(p) for p in name_parts(test_name))


def tab_name(cls, meth, entity=False):
    """PascalCase function name like the FPTU sample (signUp -> SignUp); entity=True puts the class's entity
    after the verb (SongService.GetByIdAsync -> GetSongById) for names several classes share."""
    if cls.startswith('Search*'):
        return 'ValidateSearchRequests'
    if meth == 'Validate':
        return 'Validate' + cls.removesuffix('Validator')
    if meth == 'ctor':
        return cls
    base = meth.removesuffix('Async')
    base = base[:1].upper() + base[1:]
    if not entity:
        return base
    ent = re.sub(r'(Service|Resolver)$', '', cls)
    verb = re.match(r'[A-Z][a-z]*', base).group(0)
    if cls == ent:  # not a service: SystemSettingKeys.Definitions -> SystemSettingKeysDefinitions
        return cls + base
    if base == verb + 'All':  # GetAll -> GetAllChoirs
        return f'{verb}All{ent}s'
    return verb + ent + base[len(verb):]


def name_tabs(rows):
    """Sheet Name = tab_name, Function Code = its capitals (SignUp -> SU), numbered when they collide."""
    plain = collections.Counter(tab_name(r['cells'][2], r['cells'][3]) for r in rows)
    codes = collections.Counter()
    for r in rows:
        cls, meth = r['cells'][2], r['cells'][3]
        bare = tab_name(cls, meth)
        # A shared name, or a lone verb (Accept, Respond), says nothing without its entity.
        lone = re.fullmatch(r'[A-Z][a-z]+', bare) is not None and bare not in ('Login', 'Logout')
        name = tab_name(cls, meth, plain[bare] > 1 or lone)
        code = ''.join(ch for ch in name if ch.isupper())
        codes[code] += 1
        r['cells'][4] = code if codes[code] == 1 else f'{code}{codes[code]}'
        r['cells'][5] = name
    assert len({r['cells'][5] for r in rows}) == len(rows), 'duplicate sheet name'
    assert len({r['cells'][4] for r in rows}) == len(rows), 'duplicate function code'


def build_rows(groups, mocks, mapping, trx=None):
    order = {g['code']: i for i, g in enumerate(mapping['groups'])}
    by_class = {c: g for g in mapping['groups'] for c in g['classes']}
    by_code = {g['code']: g for g in mapping['groups']}
    unmapped = sorted({cls for (_, cls, _) in groups if cls not in by_class})
    if unmapped:
        return None, unmapped
    items = []
    for (fn, cls, meth), g in groups.items():
        grp = by_code[mapping['methodOverrides'].get(f'{cls}.{meth}', by_class[cls]['code'])]
        items.append((order[grp['code']], grp, fn, cls, meth, g))
    items.sort(key=lambda x: x[0])  # stable: keeps test-file order inside a group
    rows, seq = [], collections.Counter()
    for _, grp, fn, cls, meth, g in items:
        seq[grp['code']] += 1
        code = f"{grp['code']}-{seq[grp['code']]:02d}"
        cases = g['cases']
        if trx is not None:
            cases = sum(len(trx.get((fn[:-3], t), [])) for t in g['tests']) or cases
        desc = (f"Verify {cls}.{meth} ({len(g['tests'])} tests / {cases} cases): "
                + '; '.join(scenario(t) for t in g['tests']))
        if cls.endswith('Validator') or cls.startswith('Search*') or cls in PURE:
            pre = 'Pure unit test: no database or server; input objects built in memory.'
        elif mocks.get(fn):
            pre = 'All dependencies mocked with NSubstitute (no DB/server): ' + ', '.join(mocks[fn]) + '.'
        else:
            pre = 'In-memory fakes, no DB/server.'
        rows.append({'cells': [len(rows) + 1, grp['name'], cls, meth, code, code, desc[:32000], pre],
                     'key': (fn, cls, meth), 'tests': g['tests']})
    name_tabs(rows)
    return rows, []


def build_cases(row, trx, test_info):
    """Executed cases of one function, in source order then execution order."""
    fn = row['key'][0][:-3]
    cases = []
    for t in sorted(row['tests'], key=lambda t: test_info[(fn, t)]['order']):
        for r in trx.get((fn, t), []):
            cases.append({'test': t, **r, 'exceptions': test_info[(fn, t)]['exceptions'],
                          'inputs': test_info[(fn, t)]['inputs']})
    return cases


def case_matrix(cases):
    """Condition groups (one per input of the call under test), return values, exceptions and each case's marks."""
    cond = collections.OrderedDict()
    ret, exc = [], []
    marks = []

    def add(lst, v):
        if v not in lst:
            lst.append(v)
        return v

    for c in cases:
        parts = name_parts(c['test'])
        m = {'cond': [], 'ret': [], 'exc': []}
        if len(parts) > 1:
            m['ret'].append(add(ret, ' '.join(words(p) for p in parts[1:])))
        args = {n: v.strip()[:60] for n, v in c['args']}
        used = set()
        for name, value in c.get('inputs', []):
            if value in args:  # a theory parameter: this case's InlineData value
                used.add(value)
                value = args[value]
            m['cond'].append((name, add(cond.setdefault(name, []), value)))
        for name, value in args.items():
            if EXPECTED_PARAM.match(name):
                m['ret'].append(add(ret, f'{name} = {value}'))
            elif name not in used:
                m['cond'].append((name, add(cond.setdefault(name, []), value)))
        for e in c['exceptions']:
            m['exc'].append(add(exc, e))
        # The scenario is no longer shown, but still tells normal from abnormal / boundary cases.
        text = ' '.join([words(parts[0])] + [v for _, v in m['cond']] + m['ret']).lower()
        m['type'] = 'A' if (m['exc'] or ABNORMAL.search(text)) else 'B' if BOUNDARY.search(text) else 'N'
        marks.append(m)
    return cond, ret, exc, marks


def git_author(repo):
    try:
        return subprocess.run(['git', '-C', repo, 'config', 'user.name'], capture_output=True, text=True,
                              check=True).stdout.strip()
    except (OSError, subprocess.CalledProcessError):
        return ''


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--repo', required=True)
    ap.add_argument('--sheet', help='Google Sheet link or id (native Sheet, shared with the service account)')
    ap.add_argument('--key', default=os.environ.get('HARMONIA_SHEETS_KEY'),
                    help='service account JSON key, kept outside the repo (default: $HARMONIA_SHEETS_KEY)')
    ap.add_argument('--trx')
    ap.add_argument('--only', default='', help='comma-separated Function Code prefixes, e.g. F1,F8-04')
    ap.add_argument('--map', default=os.path.join(HERE, 'requirement_map.json'))
    ap.add_argument('--dry-run', action='store_true')
    args = ap.parse_args()
    sys.stdout.reconfigure(encoding='utf-8')

    mapping = json.load(open(args.map, encoding='utf-8'))
    groups, mocks, unresolved, test_info = scan_tests(args.repo)
    trx = read_trx(args.trx) if args.trx else None
    rows, unmapped = build_rows(groups, mocks, mapping, trx)
    if unmapped:
        print('UNMAPPED CLASSES (add them to requirement_map.json):', *unmapped, sep='\n  ')
        sys.exit(2)
    assert len({r['cells'][4] for r in rows}) == len(rows), 'duplicate function code'

    tab_cases = locs = None
    if trx is not None:
        only = [p.strip() for p in args.only.split(',') if p.strip()]

        def wanted(code):  # 'F1' matches F1-xx but not F10-xx
            return not only or any(code == p or code.startswith(p + '-') for p in only)

        tab_cases = {r['cells'][4]: build_cases(r, trx, test_info) for r in rows if wanted(r['cells'][4])}
        locs = {r['cells'][4]: method_loc(args.repo, r['cells'][2], r['cells'][3]) for r in rows}
        known = {(fn[:-3], t) for r in rows for fn in [r['key'][0]] for t in r['tests']}
        orphans = sorted(f'{c}.{m}' for (c, m) in trx if (c, m) not in known)
        if orphans:
            print('TRX RESULTS WITHOUT A SOURCE TEST (rebuild the tests?):', *orphans[:20], sep='\n  ')

    tests = sum(len(g['tests']) for g in groups.values())
    cases = sum(len(v) for v in trx.values()) if trx else sum(g['cases'] for g in groups.values())
    print(f'{len(rows)} functions, {tests} tests, {cases} cases')
    for code, n in collections.Counter(r['cells'][1].split('.')[0] for r in rows).items():
        print(f'  {code}: {n}')
    if args.dry_run:
        for r in rows:
            extra = f" | {len(tab_cases[r['cells'][4]])} cases" if tab_cases and r['cells'][4] in tab_cases else ''
            print(' | '.join(str(x) for x in r['cells'][:6]) + extra)
    else:
        if not args.sheet or not args.key:
            sys.exit('--sheet and --key (or HARMONIA_SHEETS_KEY) are required unless --dry-run')
        sys.path.insert(0, HERE)  # python -P leaves the script dir off sys.path
        import sheets_writer
        ssid = sheets_writer.sheet_id_from(args.sheet)
        summary = None
        if trx:
            results = [x for v in trx.values() for x in v]
            dates = [x['date'] for x in results if x['date']]
            summary = {'functions': len(rows), 'cases': len(results),
                       'passed': sum(x['outcome'] == 'Passed' for x in results),
                       'failed': sum(x['outcome'] == 'Failed' for x in results),
                       'date': max(dates).strftime('%d/%m/%Y') if dates else '', 'version': '1.0'}
        sheets_writer.write_sheet(sheets_writer.connect(args.key), ssid, rows,
                                  {**mapping['project'], 'groups': mapping['groups']}, tab_cases,
                                  git_author(args.repo), locs or {}, summary)
        print(f'Wrote rows {FIRST_ROW}..{FIRST_ROW + len(rows) - 1} of sheet Functions in {ssid}'
              + (f' and {len(tab_cases)} function tabs' if tab_cases is not None else ''))
    if unresolved:
        print('UNRESOLVED TESTS (no method found, listed under ?<prefix>):', *unresolved, sep='\n  ')
        sys.exit(2)


if __name__ == '__main__':
    main()
