import { useState } from "react";
import { Card } from "../components/ui/Card";
import { Button } from "../components/ui/Button";
import { Input } from "../components/ui/Input";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../components/ui/Table";
import { Upload, Trash2 } from "lucide-react";

interface Video {
  id: string;
  name: string;
  size: string;
  uploadDate: string;
  device: string;
  startDate: string;
  endDate: string;
}

export function VideosPage() {
  const [videos, setVideos] = useState<Video[]>([
    {
      id: "1",
      name: "promo_summer.mp4",
      size: "15.3 MB",
      uploadDate: "2026-05-01",
      device: "VM001",
      startDate: "2026-05-01",
      endDate: "2026-05-31",
    },
    {
      id: "2",
      name: "brand_intro.mp4",
      size: "22.7 MB",
      uploadDate: "2026-05-03",
      device: "VM002",
      startDate: "2026-05-05",
      endDate: "2026-06-05",
    },
  ]);

  const handleDelete = (id: string) => {
    if (confirm("Bạn có chắc muốn xóa video này?")) {
      setVideos(videos.filter((v) => v.id !== id));
    }
  };

  return (
    <div className="p-6 space-y-6">
      <h2>Videos Management</h2>

      <Card>
        <h4 className="mb-4">Upload Video</h4>
        <form className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label className="block mb-2 text-foreground">Chọn file video</label>
              <div className="border-2 border-dashed border-border rounded-lg p-8 text-center hover:border-primary transition-colors cursor-pointer">
                <Upload size={32} className="mx-auto mb-2 text-muted-foreground" />
                <p className="text-sm text-muted-foreground">Click hoặc kéo file vào đây</p>
                <p className="text-xs text-muted-foreground mt-1">MP4, AVI, MOV (Max 100MB)</p>
              </div>
            </div>

            <div className="space-y-4">
              <div>
                <label className="block mb-2 text-foreground">Chọn thiết bị</label>
                <select className="w-full bg-input-background border-2 border-border rounded-lg px-4 py-2 text-foreground focus:outline-none focus:ring-2 focus:ring-ring focus:border-ring">
                  <option value="">Chọn thiết bị...</option>
                  <option value="VM001">VM001 - Tầng 1, Tòa nhà A</option>
                  <option value="VM002">VM002 - Tầng 2, Tòa nhà B</option>
                  <option value="VM003">VM003 - Tầng 3, Tòa nhà C</option>
                </select>
              </div>

              <Input label="Ngày bắt đầu" type="date" />
              <Input label="Ngày kết thúc" type="date" />

              <Button type="submit" className="w-full">
                Upload Video
              </Button>
            </div>
          </div>
        </form>
      </Card>

      <Card>
        <h4 className="mb-4">Danh sách Video</h4>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Tên video</TableHead>
              <TableHead>Dung lượng</TableHead>
              <TableHead>Ngày upload</TableHead>
              <TableHead>Thiết bị</TableHead>
              <TableHead>Thời gian phát</TableHead>
              <TableHead>Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {videos.map((video) => (
              <TableRow key={video.id}>
                <TableCell>{video.name}</TableCell>
                <TableCell>{video.size}</TableCell>
                <TableCell>{video.uploadDate}</TableCell>
                <TableCell>{video.device}</TableCell>
                <TableCell className="text-sm">
                  {video.startDate} - {video.endDate}
                </TableCell>
                <TableCell>
                  <Button
                    variant="ghost"
                    onClick={() => handleDelete(video.id)}
                    className="text-destructive hover:bg-destructive/10"
                  >
                    <Trash2 size={16} />
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Card>
    </div>
  );
}
