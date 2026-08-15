import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import styles from "./Charts.module.css";

export interface ValuePoint {
  date: string;
  value: number;
}

interface Props {
  data: ValuePoint[];
  currency: string;
  emptyMessage?: string;
}

const dateFormatter = new Intl.DateTimeFormat("uk-UA", { day: "2-digit", month: "short" });

export function ValueOverTimeChart({ data, currency, emptyMessage }: Props) {
  if (data.length < 2) {
    return (
      <p className={styles.empty}>
        {emptyMessage ??
          "Потрібно принаймні дві точки в часі (онови вартість пізніше, щоб побачити динаміку)."}
      </p>
    );
  }

  return (
    <ResponsiveContainer width="100%" height={220}>
      <LineChart data={data} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
        <CartesianGrid stroke="var(--border)" vertical={false} />
        <XAxis
          dataKey="date"
          tickFormatter={(d) => dateFormatter.format(new Date(d))}
          stroke="var(--text-faint)"
          tick={{ fill: "var(--text-faint)", fontSize: 12 }}
          axisLine={{ stroke: "var(--border)" }}
          tickLine={false}
        />
        <YAxis
          width={0}
          tick={false}
          axisLine={false}
          tickLine={false}
          domain={["dataMin", "dataMax"]}
        />
        <Tooltip
          formatter={(value) => `${Number(value).toLocaleString("uk-UA")} ${currency}`}
          labelFormatter={(d) => dateFormatter.format(new Date(String(d)))}
          contentStyle={{
            background: "var(--surface)",
            border: "1px solid var(--border)",
            borderRadius: 10,
            color: "var(--text)",
          }}
        />
        <Line
          type="monotone"
          dataKey="value"
          stroke="var(--accent)"
          strokeWidth={2}
          dot={{ r: 3, fill: "var(--accent)", strokeWidth: 0 }}
          activeDot={{ r: 5 }}
        />
      </LineChart>
    </ResponsiveContainer>
  );
}
