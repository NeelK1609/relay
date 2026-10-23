import { cookies } from "next/headers";

// Demo identity only: replaced by Entra External ID (customers) and Entra ID (staff) in the Azure phase.
const CUSTOMER = "relay_demo_customer";
const STAFF = "relay_demo_staff";
const options = { httpOnly: true, sameSite: "lax" as const, path: "/", maxAge: 60 * 60 * 24 * 7 };

export async function getCustomerId() {
  return (await cookies()).get(CUSTOMER)?.value;
}

export async function setCustomerId(id: string | null) {
  const jar = await cookies();
  if (id) jar.set(CUSTOMER, id, options);
  else jar.delete(CUSTOMER);
}

export async function getStaffId() {
  return (await cookies()).get(STAFF)?.value;
}

export async function setStaffId(id: string) {
  (await cookies()).set(STAFF, id, options);
}
