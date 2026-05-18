# Remaining Review Issues

Ngày tạo: 2026-05-18

File này lưu các vấn đề còn lại sau các bước đã xử lý:
- Password hashing đã chuyển sang `PasswordHasher` và có fallback migrate SHA256 cũ.
- MVC auth đã có Cookie Authentication, `[Authorize]`, CSRF token cho form/AJAX chính.
- Data leak ở `/` và `/dashboard` đã chặn bằng redirect theo role.

## 1. Xác thực device/mobile API còn yếu

Vị trí liên quan:
- `VendingAdSolution/VendingAdSystem/Controllers/PortalApiController.cs`
- `VendingAdSolution/VendingAdSystem/Controllers/MobileApiController.cs`

Vấn đề:
- `GET /api/portal/playlist/{deviceCode}`, `POST /api/portal/heartbeat`, `POST /api/portal/devices/register` hiện chủ yếu tin vào `DeviceCode`.
- `MobileApiController` cũng cho lấy device/playback state bằng `deviceCode`.
- Kẻ khác đoán/biết `deviceCode` có thể giả heartbeat hoặc đọc trạng thái phát nếu endpoint public.

Hướng xử lý đề xuất:
- Tách rõ Portal API cho web user và Device API cho thiết bị.
- Khi register thiết bị, cấp một `DeviceSecret` hoặc token riêng cho thiết bị.
- Các request từ thiết bị phải gửi `DeviceCode` + chữ ký HMAC hoặc bearer token device.
- Lưu hash của device secret, hỗ trợ rotate/revoke.
- Áp rate limit cho tất cả endpoint public theo device/IP, không chỉ heartbeat.

## 2. Portal upload API vẫn phụ thuộc session và `userId` từ client

Vị trí liên quan:
- `VendingAdSolution/VendingAdSystem/Controllers/PortalApiController.cs`
- `VendingAdSolution/VendingAdSystem/Views/Portal/Videos.cshtml`

Vấn đề:
- Upload nhận `[FromForm] int userId` từ browser, rồi so với session.
- View render `var userId = @Context.Session.GetInt32("UserId");`.
- Sau khi đã có Cookie Auth, API nên lấy user từ claim/server-side context thay vì tin dữ liệu client gửi lên.

Hướng xử lý đề xuất:
- Thêm `[Authorize(Roles = "User")]` cho các Portal API chỉ dành cho portal user.
- Inject/use `ICurrentSession` hoặc đọc claim `UserId` trong controller.
- Bỏ `userId` khỏi form data upload.
- Với admin upload thay user nếu cần, tạo endpoint/admin flow riêng và audit rõ.

## 3. Mật khẩu mặc định cố định cho user mới/reset

Vị trí liên quan:
- `VendingAdSolution/VendingAdSystem/Controllers/AdminController.cs`
- `VendingAdSolution/VendingAdSystem/Views/AdminUsers/Index.cshtml`

Vấn đề:
- Tạo user và reset password đang dùng chung mật khẩu `TD@12345`.
- Dù đã hash tốt hơn, default password cố định vẫn là rủi ro vận hành.

Hướng xử lý đề xuất:
- Generate mật khẩu tạm thời ngẫu nhiên, chỉ hiển thị một lần cho admin.
- Thêm cờ `MustChangePassword` cho user sau khi tạo/reset.
- Bắt đổi password ngay lần login đầu tiên.
- Về lâu dài, thay reset password bằng reset link/token hết hạn.

## 4. Upload video cần validation và lưu trữ chặt hơn

Vị trí liên quan:
- `VendingAdSolution/VendingAd.Application/Application/Services/MediaUploadService.cs`

Vấn đề:
- Hiện mới kiểm tra null và size 50MB.
- File extension lấy từ tên file gốc, chưa có allow-list rõ ràng.
- Chưa xác minh MIME/content thực tế, duration/codec, hoặc scan malware.
- `FileUrl` lưu absolute URL theo request host, có thể khó đổi domain/proxy về sau.

Hướng xử lý đề xuất:
- Allow-list extension: `.mp4`, `.mov`, `.avi` nếu thật sự hỗ trợ.
- Kiểm tra MIME và magic bytes cơ bản.
- Dùng media probe như FFmpeg/ffprobe để xác nhận file video hợp lệ.
- Lưu relative path trong DB, build absolute URL ở layer response/view.
- Dọn file orphan nếu DB save lỗi hoặc khi delete fail giữa chừng.

## 5. Session/manual auth check còn lẫn với Cookie Auth

Vị trí liên quan:
- `VendingAdSolution/VendingAdSystem/Controllers/AdminController.cs`
- `VendingAdSolution/VendingAdSystem/Controllers/PortalController.cs`
- `VendingAdSolution/VendingAd.Application/Application/Services/CurrentSession.cs`

Vấn đề:
- Đã có `[Authorize]`, nhưng nhiều action vẫn tự check `_currentSession`.
- Điều này làm code lặp, dễ lệch hành vi redirect/401/403 giữa các action.

Hướng xử lý đề xuất:
- Dần chuyển sang policy/role-based authorization.
- Giữ `ICurrentSession` chủ yếu để lấy `UserId/AdminId`, không dùng để quyết định auth ở từng action.
- Tạo helper lấy current user id từ claim, trả lỗi nhất quán nếu claim thiếu.
- Thêm integration test cho admin/user/anonymous trên các route quan trọng.

## 6. Config/secrets và demo seed cần hardening

Vị trí liên quan:
- `VendingAdSolution/VendingAdSystem/appsettings.json`
- `VendingAdSolution/VendingAd.Infrastructure/Infrastructure/Seed/DatabaseSeeder.cs`

Vấn đề:
- `Seed:EnableDemoData` đang bật trong config mặc định.
- RabbitMQ username/password mẫu nằm trong `appsettings.json`.
- Dễ bị deploy nhầm demo data hoặc secret mặc định.

Hướng xử lý đề xuất:
- Đưa secret sang environment variables/user secrets/secret manager.
- Tắt seed demo data mặc định, chỉ bật qua config development.
- Thêm startup validation: production không được dùng credential mặc định.
- Tách `appsettings.Development.json` khỏi config production.

## 7. Thiếu test bảo mật/integration test

Vấn đề:
- Hiện test chủ yếu kiểm tra service logic.
- Những lỗi vừa fix như data leak route `/dashboard`, CSRF, role redirect chưa có test chống regress.

Hướng xử lý đề xuất:
- Thêm `WebApplicationFactory` integration tests.
- Test anonymous vào `/admin` phải redirect login.
- Test user thường vào `/admin` không được thấy admin data.
- Test `/dashboard` với user phải redirect `/portal/dashboard`, admin phải redirect `/admin`.
- Test POST MVC thiếu anti-forgery token phải fail.
- Test upload không được nhận `userId` giả sau khi refactor.

## 8. Audit log cho thao tác nhạy cảm

Vị trí liên quan:
- Login/logout trong `AccountController`.
- Create/reset user trong `AdminController`.
- Upload/delete video và schedule changes trong `PortalController`/`PortalApiController`.

Vấn đề:
- Các thao tác nhạy cảm hiện chưa có audit trail rõ ràng.

Hướng xử lý đề xuất:
- Tạo bảng `AuditLogs` hoặc event log.
- Ghi actor id/role, action, target id, timestamp, IP/user-agent.
- Không log password/token/raw secret.
- Ưu tiên trước cho login failed, reset password, delete video, assign device.
