import { Cell, Legend, Pie, PieChart, ResponsiveContainer, Tooltip } from "recharts";
import type { PieLabelRenderProps } from "recharts";
import type { Slice } from "./chartColors";
import styles from "./Charts.module.css";

/** Below this the label has nowhere to sit without touching its neighbour; the legend
 *  still names the slice, so nothing is lost. */
const LABEL_MIN_SHARE = 0.05;

/**
 * Hand-rolled because recharts paints its default label in the slice's own colour, and
 * text belongs to the text tokens — a coloured swatch beside it carries identity, the
 * words don't have to.
 */
function SliceLabel({ cx, cy, midAngle, outerRadius, name, percent }: PieLabelRenderProps) {
  const share = percent ?? 0;
  if (share < LABEL_MIN_SHARE) {
    return null;
  }

  // recharts types every one of these as optional (and the coordinates as number | string),
  // so they are coerced rather than trusted.
  const centerX = Number(cx ?? 0);
  const centerY = Number(cy ?? 0);
  const distance = Number(outerRadius ?? 0) + 24;
  const radians = -(midAngle ?? 0) * (Math.PI / 180);
  const x = centerX + distance * Math.cos(radians);
  const y = centerY + distance * Math.sin(radians);

  return (
    <text
      x={x}
      y={y}
      fill="var(--text-dim)"
      fontSize={12}
      textAnchor={x > centerX ? "start" : "end"}
      dominantBaseline="central"
    >
      {`${name} ${Math.round(share * 100)}%`}
    </text>
  );
}

interface Props {
  slices: Slice[];
  currency: string;
  size?: "normal" | "large";
  emptyMessage?: string;
}

/**
 * One donut for the whole app: the dashboard splits capital by account type, the account
 * page splits an account by holding. Identity comes in already coloured (see chartColors)
 * so this only draws — the ring's 2px surface gap and the per-slice labels are the
 * secondary encoding that keeps it readable when the colours alone aren't enough.
 */
export function DonutChart({ slices, currency, size = "normal", emptyMessage }: Props) {
  if (slices.length === 0) {
    return <p className={styles.empty}>{emptyMessage ?? "Ще немає активів для розподілу."}</p>;
  }

  const isLarge = size === "large";

  return (
    <ResponsiveContainer width="100%" height={isLarge ? 420 : 260}>
      <PieChart>
        <Pie
          data={slices}
          dataKey="value"
          nameKey="name"
          innerRadius={isLarge ? 100 : 60}
          outerRadius={isLarge ? 160 : 95}
          paddingAngle={2}
          // Off deliberately: the donut re-mounts on every navigation and on every
          // refetch after a transaction is saved, and a pie that spins up each time
          // reads as the data changing when it hasn't.
          isAnimationActive={false}
          strokeWidth={2}
          stroke="var(--surface)"
          label={SliceLabel}
          labelLine={false}
        >
          {slices.map((slice) => (
            <Cell key={slice.name} fill={slice.color} />
          ))}
        </Pie>
        <Tooltip
          formatter={(value) => `${Number(value).toLocaleString("uk-UA")} ${currency}`}
          contentStyle={{
            background: "var(--surface)",
            border: "1px solid var(--border)",
            borderRadius: 10,
            color: "var(--text)",
          }}
        />
        <Legend
          verticalAlign="bottom"
          formatter={(value) => <span style={{ color: "var(--text-dim)" }}>{value}</span>}
        />
      </PieChart>
    </ResponsiveContainer>
  );
}
