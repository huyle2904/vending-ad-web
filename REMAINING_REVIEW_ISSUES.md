# Remaining Review Issues

Ngày cập nhật: 2026-05-19

Context vận hành hiện tại: hệ thống chưa hướng tới nhiều user; dự kiến 5-10 user, nhưng mỗi user có thể có 50+ thiết bị. Vì vậy ưu tiên các rủi ro có thể làm lộ dữ liệu thiết bị/user hoặc làm deploy nhầm config. Các hạng mục enterprise như object storage, SIEM, OpenTelemetry đầy đủ, outbox/DLQ nâng cao chưa cần đẩy lên đầu.

## Đã xử lý trong lượt này

### Device/mobile API không còn chỉ tin `DeviceCode`

Đã thêm:
- `Device.DeviceSecretHash`
- `Device.DeviceSecretCreatedAt`
- `IDeviceCredentialService`
- migration `20260519000000_AddDeviceSecrets`

Các endpoint mobile dưới `/api/mobile/*` hiện yêu cầu `X-Device-Secret` hoặc `Authorization: Bearer <secret>`. Endpoint register device trả `DeviceSecret` một lần để provisioning.

Portal device-wall endpoint vẫn cho user đã đăng nhập và sở hữu device truy cập để giữ luồng test tay, nhưng request không đăng nhập phải có device secret hợp lệ.

### Portal upload API không nhận/trust `userId` từ client

`POST /api/portal/upload` đã dùng `ICurrentSession.UserId` từ server-side context và có `[Authorize(Roles = "User")]`. Frontend upload hiện chỉ gửi file.

### Upload video đã có validation cơ bản

Đã bổ sung:
- Allow-list extension: `.mp4`, `.mov`, `.webm`
- MIME/content-type check ở mức pragmatic
- Magic bytes check cơ bản
- Sanitize file name bằng `Path.GetFileName`
- Lưu `FileUrl` dạng relative path `/uploads/...`

### Config/secrets mặc định đã cứng hơn

Đã đổi:
- `Seed:EnableDemoData=false` trong `appsettings.json`
- RabbitMQ username/password mặc định không còn nằm trong web/worker `appsettings.json`
- Startup fail nếu bật demo seed ngoài môi trường `Development`
- README local setup đã yêu cầu set environment variables rõ ràng

### Security/integration test baseline đã có

Đã thêm `WebApplicationFactory` tests cho:
- Anonymous vào `/admin` bị redirect login.
- User role không xem được admin dashboard.
- Mobile API thiếu/wrong device secret trả `401`.
- Portal upload bỏ qua `userId` giả từ form và dùng user id từ auth context.
- Mobile device-info và portal playlist bị rate limit khi vượt quota theo `deviceCode`.

### Device-facing API đã có rate limit rộng hơn

Đã áp rate limit theo `deviceCode` cho:
- `GET /api/mobile/devices/{deviceCode}`
- `POST /api/mobile/heartbeat`
- `GET /api/mobile/playback-state/{deviceCode}`
- `GET /api/portal/playlist/{deviceCode}`
- `POST /api/portal/heartbeat`

## 1. Mật khẩu mặc định cố định cho user mới/reset

Vị trí liên quan:
- `VendingAdSolution/VendingAdSystem/Controllers/AdminController.cs`
- `VendingAdSolution/VendingAdSystem/Views/AdminUsers/Index.cshtml`

Vấn đề:
- Tạo user và reset password vẫn dùng chung mật khẩu `TD@12345`.
- Với số user ít vẫn nguy hiểm, vì chỉ cần một user quên đổi password là có thể bị truy cập trái phép.
- Chủ project đã quyết định chưa cần xử lý ở giai đoạn hiện tại.

Nên làm tiếp:
- Generate mật khẩu tạm thời ngẫu nhiên, chỉ hiển thị một lần cho admin.
- Thêm cờ `MustChangePassword`.
- Bắt đổi password sau lần login đầu tiên.

## 2. Device secret lifecycle còn thiếu

Vị trí liên quan:
- `VendingAdSolution/VendingAd.Application/Application/Services/DeviceCredentialService.cs`
- `VendingAdSolution/VendingAdSystem/Controllers/PortalApiController.cs`
- `VendingAdSolution/VendingAdSystem/Controllers/MobileApiController.cs`

Vấn đề còn lại:
- Chưa có UI/API rotate secret khi thiết bị bị lộ secret.
- Chưa có revoke riêng; hiện chỉ có thể vô hiệu hóa device.

Nên làm tiếp:
- Thêm admin action `Rotate device secret`, trả secret mới một lần.
- Log audit khi register/rotate/revoke device secret.

## 3. Session/manual auth check còn lẫn với Cookie Auth

Vị trí liên quan:
- `VendingAdSolution/VendingAdSystem/Controllers/AdminController.cs`
- `VendingAdSolution/VendingAdSystem/Controllers/PortalController.cs`
- `VendingAd.Application/Application/Services/CurrentSession.cs`

Vấn đề:
- Đã có Cookie Auth và `[Authorize]`, nhưng nhiều action vẫn tự check `_currentSession`.
- Code dễ bị lệch hành vi redirect/401/403 giữa các action.

Nên làm tiếp:
- Dần chuyển sang policy/role-based authorization.
- Giữ `ICurrentSession` chủ yếu để lấy `UserId/AdminId`.
- Thêm integration test cho anonymous/user/admin trên các route chính.

## 4. Upload video cần validation sâu hơn nếu chạy thật với file từ user

Vị trí liên quan:
- `VendingAd.Application/Application/Services/MediaUploadService.cs`

Vấn đề còn lại:
- Chưa probe duration/codec bằng FFmpeg/ffprobe.
- Chưa scan malware.
- Chưa có cleanup job cho orphan file nếu lỗi xảy ra giữa DB và filesystem.

Nên làm tiếp:
- Với quy mô hiện tại, `ffprobe` để xác nhận duration/codec là bước đủ thực dụng tiếp theo.
- Malware scan/object storage/CDN để sau khi có nhu cầu public upload rộng hơn.

## 5. Thiếu security/integration tests

Vấn đề:
- Đã có baseline integration tests cho auth boundary, mobile device secret, và upload không trust `userId`.
- Vẫn thiếu coverage cho một số route/auth flow còn lại.

Nên làm tiếp:
- Test `/dashboard` redirect đúng theo role.
- Test anonymous/user/admin trên các portal/admin route còn lại.
- Test POST MVC thiếu anti-forgery token phải fail.
- Test positive path mobile API với đúng device secret.

## 6. Audit log cho thao tác nhạy cảm

Vị trí liên quan:
- Login/logout trong `AccountController`
- Create/reset user trong `AdminController`
- Register/rotate/revoke device secret
- Upload/delete video
- Schedule changes

Vấn đề:
- Chưa có audit trail rõ ràng khi có sự cố vận hành.

Nên làm tiếp:
- Tạo bảng `AuditLogs`.
- Ghi actor id/role, action, target id, timestamp, IP/user-agent.
- Không log password/token/raw secret.

## 7. Production secret management/deploy discipline

Vấn đề:
- Config mặc định đã cứng hơn, nhưng production thật vẫn cần quy trình inject secret rõ ràng.

Nên làm tiếp:
- Dùng environment variables, user-secrets local, hoặc secret manager theo nền tảng deploy.
- Không commit `appsettings.Development.json` chứa secret thật.
- CI/CD set `ConnectionStrings`, RabbitMQ, Redis qua repository/environment secrets.
