import type { AccountType } from "../accounts/types";

export interface AttributeField {
  key: string;
  label: string;
  type: "text" | "date" | "number";
}

// Suggested fields per account type — purely a UX aid for the form. Storage
// itself is a flat key-value map (see Holding.Attributes on the backend), so
// nothing here needs a migration to change; these are just starting rows,
// and the user can still add arbitrary custom fields beyond the template.
export const ATTRIBUTE_TEMPLATES: Record<AccountType, AttributeField[]> = {
  RealEstate: [
    { key: "developer", label: "Забудовник", type: "text" },
    { key: "address", label: "Адреса", type: "text" },
    { key: "contractNumber", label: "Номер договору", type: "text" },
    { key: "contractDate", label: "Дата договору", type: "date" },
    { key: "areaSqm", label: "Площа, м²", type: "number" },
  ],
  Brokerage: [{ key: "service", label: "Сервіс/брокер", type: "text" }],
  Crypto: [{ key: "exchange", label: "Біржа/гаманець", type: "text" }],
  Bank: [
    { key: "accountNumber", label: "Номер рахунку", type: "text" },
    { key: "interestRate", label: "Відсоткова ставка, %", type: "number" },
    { key: "maturityDate", label: "Дата закінчення", type: "date" },
  ],
  Cash: [{ key: "location", label: "Місце зберігання", type: "text" }],
  Other: [],
};

export const SECRET_ATTRIBUTE_TEMPLATES: Record<AccountType, AttributeField[]> = {
  RealEstate: [],
  Brokerage: [
    { key: "login", label: "Логін", type: "text" },
    { key: "password", label: "Пароль", type: "text" },
  ],
  Crypto: [
    { key: "login", label: "Логін", type: "text" },
    { key: "password", label: "Пароль", type: "text" },
  ],
  Bank: [],
  Cash: [],
  Other: [],
};
