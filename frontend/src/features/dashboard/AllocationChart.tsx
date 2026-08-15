import { Cell, Legend, Pie, PieChart, ResponsiveContainer, Tooltip } from "recharts";
import { ACCOUNT_TYPE_COLORS } from "../accounts/accountTypeColors";
import { ACCOUNT_TYPE_LABELS } from "../accounts/types";
import type { AllocationItem } from "./useDashboardSummary";
import styles from "../../shared/ui/Charts.module.css";

interface Props {
  data: AllocationItem[];
  currency: string;
  size?: "normal" | "large";
}

export function AllocationChart({ data, currency, size = "normal" }: Props) {
  if (data.length === 0) {
    return <p className={styles.empty}>Ще немає активів для розподілу.</p>;
  }

  const chartData = data.map((item) => ({
    name: ACCOUNT_TYPE_LABELS[item.type],
    value: item.value,
    color: ACCOUNT_TYPE_COLORS[item.type],
  }));

  const isLarge = size === "large";
  const height = isLarge ? 420 : 260;
  const innerRadius = isLarge ? 100 : 60;
  const outerRadius = isLarge ? 160 : 95;

  return (
    <ResponsiveContainer width="100%" height={height}>
      <PieChart>
        <Pie
          data={chartData}
          dataKey="value"
          nameKey="name"
          innerRadius={innerRadius}
          outerRadius={outerRadius}
          paddingAngle={2}
          strokeWidth={2}
          stroke="var(--surface)"
          label={({ name, percent }) => `${name} ${((percent ?? 0) * 100).toFixed(0)}%`}
          labelLine={false}
        >
          {chartData.map((entry) => (
            <Cell key={entry.name} fill={entry.color} />
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
