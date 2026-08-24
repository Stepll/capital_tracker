import { Outlet } from "react-router-dom";
import { AppBar } from "./AppBar";

/** Every authenticated page renders under the same pinned bar. */
export function AppLayout() {
  return (
    <>
      <AppBar />
      <Outlet />
    </>
  );
}
