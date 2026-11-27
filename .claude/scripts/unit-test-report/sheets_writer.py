"""Writes the Report5 rows and function tabs straight into a native Google Sheet (Sheets API v4).

Reads the template tab's layout once, then every tab is a fresh duplicate of it, so all row/column
positions are computed locally and the requests go out in a few large batchUpdate calls.
"""
import datetime
import random
import re

from build_functions_sheet import FIRST_ROW, PRECONDITION, SAMPLE_TABS, case_matrix

SCOPES = ['https://www.googleapis.com/auth/spreadsheets']
TMP_TEMPLATE = '__template__'
TABS_PER_BATCH = 15  # keeps each batchUpdate well under the request size limit
PX_PER_CHAR = 7.5


def sheet_id_from(link):
    m = re.search(r'/spreadsheets/d/([A-Za-z0-9_-]+)', link)
    return m.group(1) if m else link.strip()


def connect(key_file):
    from google.oauth2 import service_account
    from googleapiclient.discovery import build
    creds = service_account.Credentials.from_service_account_file(key_file, scopes=SCOPES)
    return build('sheets', 'v4', credentials=creds, cache_discovery=False).spreadsheets()


# ---------- cell / range helpers (0-based, end-exclusive like the API) ----------

def rng(sid, r0, r1, c0, c1):
    return {'sheetId': sid, 'startRowIndex': r0, 'endRowIndex': r1, 'startColumnIndex': c0, 'endColumnIndex': c1}


def cell(v):
    if v is None or v == '':
        return {}
    if isinstance(v, bool):
        return {'userEnteredValue': {'boolValue': v}}
    if isinstance(v, (int, float)):
        return {'userEnteredValue': {'numberValue': v}}
    return {'userEnteredValue': {'stringValue': str(v)}}


def formula(f):
    """Only formulas built here; test-derived text starting with '=' stays text."""
    return {'userEnteredValue': {'formulaValue': f}}


def write(sid, r0, c0, grid, fields='userEnteredValue'):
    """Values only, so the template's format stays. Row/col are 0-based."""
    rows = [{'values': [cell(v) if not isinstance(v, dict) else v for v in line]} for line in grid]
    return {'updateCells': {'start': {'sheetId': sid, 'rowIndex': r0, 'columnIndex': c0}, 'rows': rows,
                            'fields': fields}}


def clear(sid, r0, r1, c0, c1, fields='userEnteredValue'):
    return {'updateCells': {'range': rng(sid, r0, r1, c0, c1), 'fields': fields}}


def copy_format(sid, src, dst):
    return {'copyPaste': {'source': src, 'destination': dst, 'pasteType': 'PASTE_FORMAT'}}


def resize_area(sid, first, count, needed, ncols):
    """Rows first..first+count-1 (0-based) become `needed` rows, keeping the area's last-row format."""
    if needed > count:
        at = first + count  # right after the area, inheriting the row above
        return [{'insertDimension': {'range': {'sheetId': sid, 'dimension': 'ROWS', 'startIndex': at,
                                               'endIndex': at + needed - count}, 'inheritFromBefore': True}},
                copy_format(sid, rng(sid, at - 1, at, 0, ncols), rng(sid, at, at + needed - count, 0, ncols))]
    if needed < count:
        return [{'deleteDimension': {'range': {'sheetId': sid, 'dimension': 'ROWS', 'startIndex': first + needed,
                                               'endIndex': first + count}}}]
    return []


def find_row(values, column, text, start=0):
    """0-based row of the first cell in `column` (0-based) equal to `text`."""
    for r in range(start, len(values)):
        line = values[r]
        if len(line) > column and str(line[column]).strip().lower() == text.lower():
            return r
    raise RuntimeError(f"label '{text}' not found in column {column + 1}")


def hyperlink(gid, text):
    return formula(f'=HYPERLINK("#gid={gid}","{text}")')


# ---------- layout of the template tab ----------

def read_layout(api, ssid, title):
    got = api.get(spreadsheetId=ssid, ranges=[f"'{title}'!A1:ZZ400"], includeGridData=True,
                  fields='sheets(properties(sheetId,gridProperties),data(rowData(values(formattedValue,userEnteredFormat.borders.right.style)),'
                         'columnMetadata(pixelSize)))').execute()['sheets'][0]
    rows = got['data'][0].get('rowData', [])
    values = [[c.get('formattedValue', '') for c in r.get('values', [])] for r in rows]
    widths = [c.get('pixelSize', 100) for c in got['data'][0].get('columnMetadata', [])]
    lay = {'cond': find_row(values, 0, 'Condition')}
    lay['head'] = lay['cond'] - 1
    lay['conf'] = find_row(values, 0, 'Confirm', lay['cond'])
    lay['exc'] = find_row(values, 1, 'Exception', lay['conf'])
    lay['log'] = find_row(values, 1, 'Log message', lay['exc'])
    lay['res'] = find_row(values, 0, 'Result', lay['log'])
    head = values[lay['head']]
    last = 5  # F, 0-based
    while last + 1 < len(head) and head[last + 1]:
        last += 1
    lay['last_col'] = last
    # Right edge of the UTCID table = the header cell with the double right border (T in the sample template).
    cells = rows[lay['head']].get('values', [])
    lay['edge'] = next((i for i in range(5, len(cells))
                        if cells[i].get('userEnteredFormat', {}).get('borders', {}).get('right', {}).get('style')
                        == 'DOUBLE'), max(last, 19))
    lay['cols'] = got['properties']['gridProperties']['columnCount']
    lay['rows'] = got['properties']['gridProperties']['rowCount']
    lay['widths'] = widths
    return lay


# ---------- one function tab ----------

def tab_requests(sid, lay, row_no, row, cases, author, loc):
    """Requests that turn a fresh duplicate of the template into this function's tab."""
    _, _, cls, meth, _, _, _, _ = row['cells']
    cond, ret, exc, marks = case_matrix(cases)
    n = max(len(cases), 1)
    reqs = []

    cond_lines = [('item', PRECONDITION), ('blank', None)]
    for g, values in cond.items():
        cond_lines.append(('group', g))
        cond_lines += [('value', (g, v)) for v in values]
    cond_lines.append(('blank', None))

    ncols = max(lay['cols'], 6 + n)
    if 6 + n > lay['cols']:
        reqs.append({'appendDimension': {'sheetId': sid, 'dimension': 'COLUMNS', 'length': 6 + n - lay['cols']}})

    # Bottom-up, so the template's row numbers stay valid while resizing.
    c, cf, e, lg, rs = lay['cond'], lay['conf'], lay['exc'], lay['log'], lay['res']
    reqs += resize_area(sid, lg + 1, rs - lg - 1, 1, ncols)
    reqs += resize_area(sid, e + 1, lg - e - 1, max(len(exc), 1), ncols)
    reqs += resize_area(sid, cf + 1, e - cf - 1, max(len(ret), 1), ncols)
    reqs += resize_area(sid, c + 1, cf - c - 1, len(cond_lines), ncols)
    cf = c + 1 + len(cond_lines)
    e = cf + 1 + max(len(ret), 1)
    lg = e + 1 + max(len(exc), 1)
    rs = lg + 2
    head = lay['head']

    # Matrix columns only (rows head..rs+3): the header block above ends at column T.
    first_c, last_c, old_last, edge = 5, 5 + n - 1, lay['last_col'], lay['edge']
    new_edge = last_c  # the table ends at the last UTCID; spare template columns are removed
    matrix = lambda c0, c1: rng(sid, head, rs + 4, c0, c1)
    if new_edge != edge:
        reqs.append(copy_format(sid, matrix(edge, edge + 1), matrix(new_edge, new_edge + 1)))
        if new_edge > edge:  # inner columns copy a middle column, not the double-bordered edge
            reqs.append(copy_format(sid, matrix(first_c + 1, first_c + 2), matrix(edge, new_edge)))
            reqs.append({'updateDimensionProperties': {
                'range': {'sheetId': sid, 'dimension': 'COLUMNS', 'startIndex': edge + 1, 'endIndex': new_edge + 1},
                'properties': {'pixelSize': lay['widths'][first_c + 1]}, 'fields': 'pixelSize'}})
        else:
            reqs.append(clear(sid, head, rs + 4, new_edge + 1, edge + 1, 'userEnteredValue,userEnteredFormat'))
    width_c = max(new_edge, edge, old_last) + 1
    # The template's 'O' list validation shows a dropdown arrow in every cell of Google Sheets, and rejects A/P.
    reqs.append({'setDataValidation': {'range': matrix(first_c, width_c)}})
    reqs.append({'autoResizeDimensions': {'dimensions': {'sheetId': sid, 'dimension': 'ROWS',
                                                         'startIndex': head, 'endIndex': head + 1}}})

    # Header block: C2/L2 point at the Functions row; drop a Text number format so the formula evaluates.
    reqs.append(write(sid, 1, 2, [[formula(f'=Functions!E{row_no}')]], 'userEnteredValue,userEnteredFormat.numberFormat'))
    reqs.append(write(sid, 1, 11, [[formula(f'=Functions!D{row_no}')]], 'userEnteredValue,userEnteredFormat.numberFormat'))
    reqs.append(write(sid, 2, 2, [[author]]))
    reqs.append(write(sid, 2, 11, [[author]]))
    reqs.append(write(sid, 3, 2, [[loc if loc is not None else '']]))
    reqs.append(write(sid, 4, 2, [[f'Verify {cls}.{meth}']]))

    # Clear the sample content, then write ours.
    reqs.append(clear(sid, head, head + 1, first_c, width_c))
    reqs.append(clear(sid, c + 1, cf, 1, width_c))
    reqs.append(clear(sid, cf, cf + 1, 2, width_c))
    reqs.append(clear(sid, cf + 1, e, 1, width_c))
    reqs.append(clear(sid, e + 1, lg, 1, width_c))
    reqs.append(clear(sid, lg + 1, rs, 1, width_c))
    reqs.append(clear(sid, rs, rs + 4, first_c, width_c))

    reqs.append(write(sid, head, first_c, [[f'UTCID{i + 1:02d}' for i in range(n)]]))

    def block(lines, mark_of):
        grid = []
        for kind, payload in lines:
            line = [''] * (4 + n)  # B, C, D, E(hidden), F..
            if kind == 'group':
                line[0] = payload
            elif kind in ('item', 'value'):
                line[2] = payload if kind == 'item' else payload[1]
            if kind not in ('blank', 'group'):
                for j, m in enumerate(marks):
                    if mark_of(kind, payload, m):
                        line[4 + j] = 'O'
            grid.append(line)
        return grid

    reqs.append(write(sid, c + 1, 1, block(cond_lines, lambda k, p, m: k == 'item' or p in m['cond'])))
    if ret:
        reqs.append(write(sid, cf + 1, 1, block([('value', ('ret', v)) for v in ret], lambda k, p, m: p[1] in m['ret'])))
    if exc:
        reqs.append(write(sid, e + 1, 1, block([('value', ('exc', v)) for v in exc], lambda k, p, m: p[1] in m['exc'])))

    if cases:
        epoch = datetime.date(1899, 12, 30)
        date_cell = lambda d: {'userEnteredValue': {'numberValue': (d - epoch).days},
                               'userEnteredFormat': {'numberFormat': {'type': 'DATE', 'pattern': 'dd/mm'}}} if d else {}
        reqs.append(write(sid, rs, first_c, [[m['type'] for m in marks],
                                            [{'Passed': 'P', 'Failed': 'F'}.get(x['outcome'], '') for x in cases]]))
        reqs.append(write(sid, rs + 2, first_c, [[date_cell(x['date']) for x in cases]],
                          'userEnteredValue,userEnteredFormat.numberFormat'))
        reqs.append({'autoResizeDimensions': {'dimensions': {'sheetId': sid, 'dimension': 'ROWS',
                                                             'startIndex': rs + 2, 'endIndex': rs + 3}}})

    # Drop the empty grid below the report: 1000 template rows x 250 tabs nears the 10M-cell file limit.
    total = lay['rows'] + (rs - lay['res'])
    if total > rs + 10:
        reqs.append({'deleteDimension': {'range': {'sheetId': sid, 'dimension': 'ROWS', 'startIndex': rs + 10,
                                                   'endIndex': total}}})

    # Values sit right-aligned in D and spill left over B:C; widen B so the longest one shows whole.
    longest = max([len(str(p[1] if isinstance(p, tuple) else p)) for k, p in cond_lines if k in ('item', 'value')]
                  + [len(v) for v in ret + exc] + [0])
    w = lay['widths']
    need = longest * PX_PER_CHAR - w[2] - w[3]
    reqs.append({'updateDimensionProperties': {
        'range': {'sheetId': sid, 'dimension': 'COLUMNS', 'startIndex': 1, 'endIndex': 2},
        'properties': {'pixelSize': int(min(640, max(w[1], need)))}, 'fields': 'pixelSize'}})
    return reqs


# ---------- whole workbook ----------

def send(api, ssid, reqs, label):
    for i in range(0, len(reqs), 4000):
        api.batchUpdate(spreadsheetId=ssid, body={'requests': reqs[i:i + 4000]}).execute(num_retries=6)
    print(f'  sent {label}: {len(reqs)} requests', flush=True)


def write_sheet(api, ssid, rows, project, tab_cases, author, locs, summary=None):
    meta = api.get(spreadsheetId=ssid, fields='sheets(properties(sheetId,title,index,gridProperties))').execute()
    sheets = {s['properties']['title']: s['properties'] for s in meta['sheets']}
    order = [s['properties']['title'] for s in sorted(meta['sheets'], key=lambda s: s['properties']['index'])]
    used_ids = {p['sheetId'] for p in sheets.values()}

    def new_id():
        while True:
            i = random.randint(10_000_000, 2_000_000_000)
            if i not in used_ids:
                used_ids.add(i)
                return i

    gid = {t: p['sheetId'] for t, p in sheets.items()}
    reqs = []

    if tab_cases is not None:
        codes = [r['cells'][4] for r in rows]
        titles = [r['cells'][5] for r in rows]
        template = 'Function 1' if 'Function 1' in sheets else next((c for c in titles + codes if c in sheets), None)
        if not template:
            raise RuntimeError("no template tab: neither 'Function 1' nor an earlier function tab exists")
        lay = read_layout(api, ssid, template)
        # Work from a private copy, so the template itself can be rebuilt or deleted.
        tpl = new_id()
        send(api, ssid, [{'duplicateSheet': {'sourceSheetId': gid[template], 'insertSheetIndex': len(order),
                                             'newSheetId': tpl, 'newSheetName': TMP_TEMPLATE}}], 'template copy')
        order.append(TMP_TEMPLATE)

        anchor, batch, built = 'Statistics', [], 0
        if len(tab_cases) == len(rows):  # full rebuild: drop function tabs left from an older naming
            others = [t for t in order if t not in titles and t not in codes
                      and t not in ('Guideline', 'Cover', 'Functions', 'Statistics', 'Example', TMP_TEMPLATE) + SAMPLE_TABS]
            if others:
                heads = api.values().batchGet(spreadsheetId=ssid, ranges=[f"'{t}'!A2" for t in others]).execute()
                for t, vr in zip(others, heads['valueRanges']):
                    if vr.get('values', [['']])[0][0].strip() == 'Function Code':
                        batch.append({'deleteSheet': {'sheetId': gid.pop(t)}})
                        order.remove(t)
        # Tabs of a rebuilt function: its current title and, from older runs, a tab named after its code.
        for i, r in enumerate(rows):
            code, title = r['cells'][4], r['cells'][5]
            if code not in tab_cases:
                if title in gid:
                    anchor = title
                continue
            for old in {title, code} - (set(titles) - {title}):
                if old in gid:
                    batch.append({'deleteSheet': {'sheetId': gid.pop(old)}})
                    order.remove(old)
            sid = new_id()
            at = order.index(anchor) + 1
            batch.append({'duplicateSheet': {'sourceSheetId': tpl, 'insertSheetIndex': at, 'newSheetId': sid,
                                             'newSheetName': title}})
            order.insert(at, title)
            gid[title] = sid
            batch += tab_requests(sid, lay, FIRST_ROW + i, r, tab_cases[code], author, locs.get(code))
            anchor, built = title, built + 1
            if built % TABS_PER_BATCH == 0:
                send(api, ssid, batch, f'tabs up to {code} {title}')
                batch = []
        if batch:
            send(api, ssid, batch, f'tabs up to {anchor}')
        reqs.append({'deleteSheet': {'sheetId': tpl}})
        for s in SAMPLE_TABS:
            if s in gid and s not in titles:
                reqs.append({'deleteSheet': {'sheetId': gid.pop(s)}})

    # Cover / Functions
    cover, fn = gid['Cover'], gid['Functions']
    reqs.append(write(cover, 3, 1, [[project['name']], [project['code']]]))
    if summary:
        reqs += header_requests(cover, gid['Statistics'], project, author, summary)
    reqs.append(write(fn, 3, 4, [[formula('=Cover!B4')], [formula('=Cover!B5')]]))
    reqs.append(write(fn, 6, 4, [[project['environment']]]))
    first = FIRST_ROW - 1
    last = first + len(rows)  # end-exclusive
    fn_rows = sheets['Functions']['gridProperties']['rowCount']
    reqs.append(clear(fn, first, fn_rows, 0, 8))
    reqs.append(clear(fn, last, fn_rows, 0, 8, 'userEnteredValue,userEnteredFormat'))
    if len(rows) > 1:
        reqs.append(copy_format(fn, rng(fn, first, first + 1, 0, 8), rng(fn, first + 1, last, 0, 8)))
    grid = []
    for r in rows:
        line = list(r['cells'])
        if line[5] in gid:
            line[5] = hyperlink(gid[line[5]], line[5])
        grid.append(line)
    reqs.append(write(fn, first, 0, grid))
    reqs.append({'repeatCell': {'range': rng(fn, first, last, 6, 8),
                                'cell': {'userEnteredFormat': {'wrapStrategy': 'WRAP'}},
                                'fields': 'userEnteredFormat.wrapStrategy'}})
    reqs.append({'repeatCell': {'range': rng(fn, first, last, 0, 8),
                                'cell': {'userEnteredFormat': {'verticalAlignment': 'TOP'}},
                                'fields': 'userEnteredFormat.verticalAlignment'}})
    reqs.append({'autoResizeDimensions': {'dimensions': {'sheetId': fn, 'dimension': 'ROWS',
                                                         'startIndex': first, 'endIndex': last}}})

    tabs = [(r['cells'][4], r['cells'][5]) for r in rows if r['cells'][5] in gid]
    if tab_cases is not None or tabs:
        reqs += statistics_requests(api, ssid, gid['Statistics'], tabs, gid)
    send(api, ssid, reqs, 'Functions / Statistics')


def statistics_requests(api, ssid, sid, tabs, gid):
    values = api.values().get(spreadsheetId=ssid, range="'Statistics'!A1:I3000").execute().get('values', [])
    head = find_row(values, 1, 'Function code')
    sub = next(r for r in range(head + 1, len(values))
               if any(len(values[r]) > c and str(values[r][c]).strip().lower() == 'sub total' for c in (0, 1)))
    first = head + 1
    reqs = resize_area(sid, first, sub - first, max(len(tabs), 1) + 1, 9)
    sub = first + max(len(tabs), 1) + 1
    reqs.append(clear(sid, first, sub, 0, 9))
    if not tabs:
        return reqs
    cells = ['A7', 'C7', 'F7', 'L7', 'M7', 'N7', 'O7']  # Passed, Failed, Untested, N, A, B, Total of each tab
    reqs.append(write(sid, first, 0, [[i + 1, hyperlink(gid[t], code)] + [formula(f"='{t}'!{c}") for c in cells]
                                      for i, (code, t) in enumerate(tabs)]))
    last = first + len(tabs)  # 1-based row of the last tab line
    reqs.append(write(sid, sub, 2, [[formula(f'=SUM({c}{first + 1}:{c}{last})') for c in 'CDEFGHI']]))
    return reqs


def header_requests(cover, stat, project, author, sm):
    """Cover (document info + record of change) and the Statistics header, replacing the template's <...> text."""
    doc_code = f"{project['code']}_Report5_Unit Test_v{sm['version']}"
    modules = [g['name'] for g in project['groups'] if g['code'] != 'COM']
    change = (f"Unit test report for {sm['functions']} functions / {sm['cases']} test cases "
              f"({sm['passed']} passed, {sm['failed']} failed) of test/Harmonia.Application.Tests")
    return [
        write(cover, 3, 5, [[author], [sm['date']], [sm['version']]]),  # F4 Creator, F5 Issue Date, F6 Version
        write(cover, 5, 1, [[doc_code]]),
        write(cover, 10, 0, [[sm['date'], sm['version'], 'Unit test report (Report5)', 'A', change,
                              'docs/harmonia-feature-list.md; test/Harmonia.Application.Tests']]),
        write(stat, 3, 5, [[author], ['']]),  # F4 Creator, F5 Reviewer/Approver (left for the reviewer)
        write(stat, 5, 1, [[doc_code]]),
        write(stat, 5, 5, [[sm['date']]]),
        write(stat, 6, 1, [[f"Release {sm['version']} includes {len(modules)} modules: " + '; '.join(modules)]]),
    ]
