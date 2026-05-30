# Hướng Dẫn Test Hệ Thống Với Facebook Thật

Tuyệt vời! Bây giờ hệ thống đã hoàn hảo ở môi trường local, bước cuối cùng là kết nối nó ra ngoài Internet để nhận bình luận từ người dùng thật trên trang Facebook của bạn.

> [!IMPORTANT]
> Tôi đã **bật lại tính năng bảo mật chữ ký (HMAC)** trong code `WebhookService` để ngăn chặn hacker gửi dữ liệu giả mạo. Hệ thống hiện tại chỉ chấp nhận dữ liệu có chữ ký mã hóa từ Facebook thật.

Hãy thực hiện theo các bước sau để test thực tế:

## Bước 1: Khởi động lại WebhookService
Vì tôi vừa sửa lại code bật bảo mật, bạn cần khởi động lại nó:
1. Vào terminal đang chạy `WebhookService`, bấm `Ctrl + C` để dừng.
2. Chạy lại lệnh: `dotnet run --project webhook-service/WebhookService/WebhookService.csproj`

## Bước 2: Đưa WebhookService ra Internet (bằng ngrok)
Facebook không thể gửi dữ liệu vào `localhost` của máy bạn. Chúng ta cần dùng `ngrok` để tạo một đường link public tạm thời.
1. Mở một terminal mới (hoặc CMD/PowerShell).
2. Chạy lệnh: `ngrok http 3001` (nếu bạn chưa cài ngrok, hãy tải tại ngrok.com).
3. Copy đường link có chữ `https` (ví dụ: `https://abcd-123.ngrok.io`).

## Bước 3: Cấu hình trên Facebook Developer
1. Trở lại trang **Facebook Developer Dashboard**, vào mục **Webhooks**.
2. Chọn **Chỉnh sửa Đăng ký (Edit Subscription)**.
3. Dán link ngrok vừa copy vào ô URL, nhưng nhớ thêm đuôi `/webhook` (ví dụ: `https://abcd-123.ngrok.io/webhook`).
4. Ô Token xác minh điền: `webhook-nhan`.
5. Bấm **Xác minh và Lưu**.

> [!WARNING]
> Đảm bảo rằng **App Secret** và **Page ID** trong file `webhook-service/WebhookService/appsettings.json` của bạn đúng là của cái App và Fanpage bạn đang cấu hình nhé!

## Bước 4: Test thực tế
1. Mở Facebook lên, vào Fanpage của bạn bằng tài khoản cá nhân.
2. Tìm một bài viết bất kỳ và để lại bình luận thật (ví dụ: *"Sản phẩm này ship về Hà Nội mất bao lâu shop ơi?"*).
3. Quay lại màn hình máy tính và theo dõi 2 cửa sổ terminal của `WebhookService` và `CoreService`.

Bạn sẽ thấy dữ liệu chạy cái vèo qua Kafka, và AI phân tích ra `Intent: ask_price` ngay lập tức! Chúc bạn test thành công!
