import { useState } from "react";
import { Card } from "../components/ui/Card";
import { Button } from "../components/ui/Button";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../components/ui/Table";
import { Key, Trash2 } from "lucide-react";

interface User {
  id: string;
  email: string;
  name: string;
  createdAt: string;
  role: "admin" | "brand";
}

export function AccountListPage() {
  const [users, setUsers] = useState<User[]>([
    {
      id: "1",
      email: "admin@vendingads.com",
      name: "Admin User",
      createdAt: "2026-01-15",
      role: "admin",
    },
    {
      id: "2",
      email: "brand1@example.com",
      name: "Brand Manager 1",
      createdAt: "2026-02-20",
      role: "brand",
    },
    {
      id: "3",
      email: "brand2@example.com",
      name: "Brand Manager 2",
      createdAt: "2026-03-10",
      role: "brand",
    },
  ]);

  const handleResetPassword = (email: string) => {
    alert(`Email khôi phục mật khẩu đã được gửi tới ${email}`);
  };

  const handleDelete = (id: string) => {
    if (confirm("Bạn có chắc muốn xóa tài khoản này?")) {
      setUsers(users.filter((u) => u.id !== id));
    }
  };

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <h2>Account Management</h2>
        <Button>Thêm tài khoản</Button>
      </div>

      <Card>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Email</TableHead>
              <TableHead>Tên</TableHead>
              <TableHead>Role</TableHead>
              <TableHead>Ngày tạo</TableHead>
              <TableHead>Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {users.map((user) => (
              <TableRow key={user.id}>
                <TableCell>{user.email}</TableCell>
                <TableCell>{user.name}</TableCell>
                <TableCell>
                  <span className={`px-2 py-1 rounded text-xs ${
                    user.role === "admin"
                      ? "bg-primary/20 text-primary"
                      : "bg-secondary text-secondary-foreground"
                  }`}>
                    {user.role}
                  </span>
                </TableCell>
                <TableCell>{user.createdAt}</TableCell>
                <TableCell>
                  <div className="flex gap-2">
                    <Button
                      variant="ghost"
                      onClick={() => handleResetPassword(user.email)}
                      className="text-foreground hover:bg-secondary"
                    >
                      <Key size={16} />
                    </Button>
                    <Button
                      variant="ghost"
                      onClick={() => handleDelete(user.id)}
                      className="text-destructive hover:bg-destructive/10"
                    >
                      <Trash2 size={16} />
                    </Button>
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Card>
    </div>
  );
}
