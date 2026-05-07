import { useState } from "react";
import { Card } from "../components/ui/Card";
import { Button } from "../components/ui/Button";
import { Modal } from "../components/ui/Modal";
import { Input } from "../components/ui/Input";
import { Monitor, MapPin, Circle } from "lucide-react";

interface Device {
  id: string;
  code: string;
  location: string;
  status: "online" | "offline";
  videoCount: number;
}

export function DevicesPage() {
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [devices, setDevices] = useState<Device[]>([
    {
      id: "1",
      code: "VM001",
      location: "Tầng 1, Tòa nhà A",
      status: "online",
      videoCount: 3,
    },
    {
      id: "2",
      code: "VM002",
      location: "Tầng 2, Tòa nhà B",
      status: "online",
      videoCount: 5,
    },
    {
      id: "3",
      code: "VM003",
      location: "Tầng 3, Tòa nhà C",
      status: "offline",
      videoCount: 2,
    },
  ]);

  const stats = {
    total: devices.length,
    online: devices.filter((d) => d.status === "online").length,
    offline: devices.filter((d) => d.status === "offline").length,
  };

  const handleDelete = (id: string) => {
    if (confirm("Bạn có chắc muốn xóa thiết bị này?")) {
      setDevices(devices.filter((d) => d.id !== id));
    }
  };

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <h2>Devices Management</h2>
        <Button onClick={() => setIsModalOpen(true)}>Thêm thiết bị</Button>
      </div>

      <div className="grid grid-cols-3 gap-4">
        <Card>
          <p className="text-muted-foreground mb-1">Tổng thiết bị</p>
          <p className="text-3xl text-foreground">{stats.total}</p>
        </Card>
        <Card>
          <p className="text-muted-foreground mb-1">Online</p>
          <p className="text-3xl text-[#16a34a]">
            {stats.online}
          </p>
        </Card>
        <Card>
          <p className="text-muted-foreground mb-1">Offline</p>
          <p className="text-3xl text-[#dc2626]">
            {stats.offline}
          </p>
        </Card>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {devices.map((device) => (
          <Card key={device.id} className="space-y-3">
            <div className="flex items-start justify-between">
              <div className="flex items-center gap-2">
                <Monitor size={20} className="text-muted-foreground" />
                <h4>{device.code}</h4>
              </div>
              <div className="flex items-center gap-1">
                <Circle
                  size={12}
                  fill={device.status === "online" ? "#16a34a" : "#dc2626"}
                  stroke="none"
                />
                <span className="text-sm text-muted-foreground capitalize">
                  {device.status}
                </span>
              </div>
            </div>

            <div className="flex items-center gap-2 text-muted-foreground text-sm">
              <MapPin size={16} />
              <span>{device.location}</span>
            </div>

            <p className="text-sm text-muted-foreground">
              {device.videoCount} video(s)
            </p>

            <div className="flex gap-2 pt-2 border-t border-border">
              <Button variant="ghost" className="flex-1 text-sm">
                Chi tiết
              </Button>
              <Button variant="ghost" className="flex-1 text-sm">
                Cập nhật video
              </Button>
              <Button
                variant="ghost"
                className="flex-1 text-sm text-destructive"
                onClick={() => handleDelete(device.id)}
              >
                Xóa
              </Button>
            </div>
          </Card>
        ))}
      </div>

      <Modal isOpen={isModalOpen} onClose={() => setIsModalOpen(false)} title="Thêm thiết bị mới">
        <form className="space-y-4">
          <Input label="Mã thiết bị" placeholder="VM004" required />
          <Input label="Vị trí" placeholder="Tầng 1, Tòa nhà D" required />
          <div className="flex gap-2 pt-2">
            <Button type="button" variant="secondary" onClick={() => setIsModalOpen(false)} className="flex-1">
              Hủy
            </Button>
            <Button type="submit" className="flex-1">
              Thêm
            </Button>
          </div>
        </form>
      </Modal>
    </div>
  );
}
