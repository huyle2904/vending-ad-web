import { createBrowserRouter } from "react-router";
import { Layout } from "./components/Layout";
import { LoginPage } from "./pages/LoginPage";
import { DevicesPage } from "./pages/DevicesPage";
import { VideosPage } from "./pages/VideosPage";
import { SettingsPage } from "./pages/SettingsPage";
import { AccountListPage } from "./pages/AccountListPage";
import { AccountProfilePage } from "./pages/AccountProfilePage";

export const router = createBrowserRouter([
  {
    path: "/login",
    Component: LoginPage,
  },
  {
    path: "/",
    Component: Layout,
    children: [
      {
        path: "brand/dashboard",
        Component: DevicesPage,
      },
      {
        path: "brand/videos",
        Component: VideosPage,
      },
      {
        path: "brand/settings",
        Component: SettingsPage,
      },
      {
        path: "admin/users",
        Component: AccountListPage,
      },
      {
        path: "account/profile",
        Component: AccountProfilePage,
      },
      {
        index: true,
        Component: DevicesPage,
      },
    ],
  },
]);
