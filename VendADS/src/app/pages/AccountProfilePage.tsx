import { useState } from "react";
import { useNavigate } from "react-router";
import { Card } from "../components/ui/Card";
import { Button } from "../components/ui/Button";
import { Input } from "../components/ui/Input";
import { LogOut } from "lucide-react";

export function AccountProfilePage() {
  const navigate = useNavigate();
  const [oldPassword, setOldPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");

  const handleChangePassword = (e: React.FormEvent) => {
    e.preventDefault();
    if (newPassword !== confirmPassword) {
      alert("Mật khẩu mới không khớp!");
      return;
    }
    alert("Đổi mật khẩu thành công!");
    setOldPassword("");
    setNewPassword("");
    setConfirmPassword("");
  };

  const handleLogout = () => {
    if (confirm("Bạn có chắc muốn đăng xuất?")) {
      navigate("/login");
    }
  };

  return (
    <div className="p-6 space-y-6">
      <h2>Account Profile</h2>

      <Card>
        <h4 className="mb-4">Thông tin tài khoản</h4>
        <div className="space-y-3">
          <div className="flex justify-between py-2 border-b border-border">
            <span className="text-muted-foreground">Email:</span>
            <span className="text-foreground">user@example.com</span>
          </div>
          <div className="flex justify-between py-2 border-b border-border">
            <span className="text-muted-foreground">Tên:</span>
            <span className="text-foreground">Brand Manager</span>
          </div>
          <div className="flex justify-between py-2 border-b border-border">
            <span className="text-muted-foreground">Role:</span>
            <span className="text-foreground">Brand</span>
          </div>
          <div className="flex justify-between py-2">
            <span className="text-muted-foreground">Ngày tạo:</span>
            <span className="text-foreground">2026-03-15</span>
          </div>
        </div>
      </Card>

      <Card>
        <h4 className="mb-4">Đổi mật khẩu</h4>
        <form onSubmit={handleChangePassword} className="space-y-4">
          <Input
            label="Mật khẩu hiện tại"
            type="password"
            placeholder="••••••••"
            value={oldPassword}
            onChange={(e) => setOldPassword(e.target.value)}
            required
          />
          <Input
            label="Mật khẩu mới"
            type="password"
            placeholder="••••••••"
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
            required
          />
          <Input
            label="Xác nhận mật khẩu mới"
            type="password"
            placeholder="••••••••"
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            required
          />
          <Button type="submit">Đổi mật khẩu</Button>
        </form>
      </Card>

      <Card>
        <Button
          variant="destructive"
          onClick={handleLogout}
          className="w-full flex items-center justify-center gap-2"
        >
          <LogOut size={20} />
          Đăng xuất
        </Button>
      </Card>
    </div>
  );
}
