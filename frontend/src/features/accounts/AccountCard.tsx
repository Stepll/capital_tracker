import { Link } from "react-router-dom";
import { ACCOUNT_TYPE_LABELS, type Account } from "./types";
import styles from "./AccountCard.module.css";

const TYPE_COLOR: Record<Account["type"], string> = {
  Bank: "#2f80ff",
  Brokerage: "#a78bfa",
  RealEstate: "#ff9f43",
  Cash: "#35d07f",
  Crypto: "#f2c94c",
  Other: "#9a9aa2",
};

interface Props {
  account: Account;
  onDelete: (id: string) => void;
}

export function AccountCard({ account, onDelete }: Props) {
  return (
    <Link to={`/accounts/${account.id}`} className={styles.card}>
      <div className={styles.icon} style={{ background: TYPE_COLOR[account.type] }}>
        {account.name.charAt(0).toUpperCase()}
      </div>
      <div className={styles.info}>
        <span className={styles.name}>{account.name}</span>
        <span className={styles.type}>{ACCOUNT_TYPE_LABELS[account.type]}</span>
      </div>
      <div className={styles.right}>
        <span className={styles.balance}>
          {account.totalValue.toLocaleString("uk-UA")} {account.currency}
        </span>
        <button
          className={styles.delete}
          onClick={(e) => {
            e.preventDefault();
            onDelete(account.id);
          }}
          aria-label="Видалити рахунок"
        >
          ✕
        </button>
      </div>
    </Link>
  );
}
