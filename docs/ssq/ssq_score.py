"""SSQ 점수 계산 (#46, docs/SSQ_TEST.md).

사용법: python docs/ssq/ssq_score.py 기록.csv
기록 형식: ssq_record_template.csv (참가자 1명 = pre, A, B 세 줄, 문항 q1~q16은 0~3)
출력: 줄마다 N·O·D·TS, 조건별 평균, 시작 전 대비 증가량(조건 TS − pre TS), A와 B 차이.
scipy가 설치되어 있으면 증가량에 Wilcoxon 부호 순위 검정도 함께 출력.
Kennedy et al. (1993) 가중치: N 9.54, O 7.58, D 13.92, TS 3.74
"""
import csv
import statistics
import sys

N_ITEMS = [1, 6, 7, 8, 9, 15, 16]
O_ITEMS = [1, 2, 3, 4, 5, 9, 11]
D_ITEMS = [5, 8, 10, 11, 12, 13, 14]


def score(row):
    q = {i: int(row[f"q{i}"]) for i in range(1, 17)}
    for i, v in q.items():
        if v not in (0, 1, 2, 3):
            raise ValueError(f"id {row['id']} {row['condition']}: q{i}={v} (0~3만 가능)")
    n, o, d = (sum(q[i] for i in items) for items in (N_ITEMS, O_ITEMS, D_ITEMS))
    return {"N": n * 9.54, "O": o * 7.58, "D": d * 13.92, "TS": (n + o + d) * 3.74}


def mean(xs):
    return statistics.mean(xs) if xs else float("nan")


def main(path):
    with open(path, encoding="utf-8-sig", newline="") as f:
        rows = [r for r in csv.DictReader(f) if r.get("id", "").strip()]
    by_id = {}
    print(f"{'id':>4} {'순서':>4} {'조건':>4}  {'N':>6} {'O':>6} {'D':>6} {'TS':>6}  {'시간(s)':>8} 포기")
    for r in rows:
        s = score(r)
        by_id.setdefault(r["id"], {})[r["condition"]] = (s, r)
        print(f"{r['id']:>4} {r['order']:>4} {r['condition']:>4}  {s['N']:6.1f} {s['O']:6.1f} {s['D']:6.1f} {s['TS']:6.1f}  {r.get('finish_time_s', ''):>8} {r.get('dropped', '')}")

    print("\n조건별 평균 TS")
    for c in ("pre", "A", "B"):
        ts = [v[c][0]["TS"] for v in by_id.values() if c in v]
        print(f"  {c:>3}: {mean(ts):6.1f}  (n={len(ts)})")

    inc_a, inc_b = [], []
    for pid, v in by_id.items():
        if all(c in v for c in ("pre", "A", "B")):
            inc_a.append(v["A"][0]["TS"] - v["pre"][0]["TS"])
            inc_b.append(v["B"][0]["TS"] - v["pre"][0]["TS"])
    print(f"\n시작 전 대비 증가량 (짝이 맞는 {len(inc_a)}명)")
    print(f"  A(옵션 끔): {mean(inc_a):6.1f}   B(옵션 켬): {mean(inc_b):6.1f}   차이 B−A: {mean([b - a for a, b in zip(inc_a, inc_b)]):6.1f}  (음수면 옵션 켬이 멀미 적음)")
    dropped = {c: sum(1 for v in by_id.values() if c in v and v[c][1].get("dropped", "0").strip() == "1") for c in ("A", "B")}
    print(f"  중도 포기: A {dropped['A']}명, B {dropped['B']}명")

    try:
        from scipy.stats import wilcoxon
        if len(inc_a) >= 5 and any(a != b for a, b in zip(inc_a, inc_b)):
            stat, p = wilcoxon(inc_a, inc_b)
            print(f"  Wilcoxon 부호 순위 검정: W={stat:.1f}, p={p:.3f}")
        else:
            print("  (검정은 5명 이상, 차이가 있을 때만)")
    except ImportError:
        print("  (scipy가 없어서 검정 생략 — pip install scipy)")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print(__doc__)
        sys.exit(1)
    main(sys.argv[1])
