import { Outlet, Link, useLocation } from "react-router";
import { Home, Video, Settings, Users, User } from "lucide-react";
import { useState } from "react";

export function Layout() {
  const location = useLocation();
  const [userRole] = useState<"admin" | "brand">("brand");

  const navigation = [
    { name: "Devices", path: "/brand/dashboard", icon: Home, roles: ["brand", "admin"] },
    { name: "Videos", path: "/brand/videos", icon: Video, roles: ["brand", "admin"] },
    { name: "Settings", path: "/brand/settings", icon: Settings, roles: ["brand", "admin"] },
    { name: "Account List", path: "/admin/users", icon: Users, roles: ["admin"] },
  ];

  const filteredNavigation = navigation.filter((item) =>
    item.roles.includes(userRole)
  );

  return (
    <div className="flex h-screen bg-background">
      <aside className="w-[220px] bg-sidebar border-r border-sidebar-border flex flex-col">
        <div className="p-6 border-b border-sidebar-border">
          <h1 className="text-primary">VendingAds</h1>
        </div>

        <nav className="flex-1 p-4 space-y-1">
          {filteredNavigation.map((item) => {
            const Icon = item.icon;
            const isActive = location.pathname === item.path;
            return (
              <Link
                key={item.path}
                to={item.path}
                className={`flex items-center gap-3 px-3 py-2 rounded-lg transition-colors ${
                  isActive
                    ? "bg-sidebar-accent text-sidebar-accent-foreground"
                    : "text-sidebar-foreground hover:bg-sidebar-accent/50"
                }`}
              >
                <Icon size={20} />
                <span>{item.name}</span>
              </Link>
            );
          })}
        </nav>

        <div className="p-4 border-t border-sidebar-border">
          <Link
            to="/account/profile"
            className={`flex items-center gap-3 px-3 py-2 rounded-lg transition-colors ${
              location.pathname === "/account/profile"
                ? "bg-sidebar-accent text-sidebar-accent-foreground"
                : "text-sidebar-foreground hover:bg-sidebar-accent/50"
            }`}
          >
            <User size={20} />
            <div className="flex flex-col">
              <span className="text-sm">Account</span>
              <span className="text-xs text-muted-foreground">user@example.com</span>
            </div>
          </Link>
        </div>
      </aside>

      <main className="flex-1 overflow-auto">
        <Outlet />
      </main>
    </div>
  );
}
