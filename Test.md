# [cite_start]Bài tập thực hành Lập trình API: Hệ thống quản lý Facebook Page phân tán [cite: 1]

## [cite_start]1. Tổng quan [cite: 2]
* [cite_start]**Học phần:** Lập trình API[cite: 3].
* [cite_start]**Ngày:** 25 tháng 4 năm 2026[cite: 4].
* [cite_start]Bài tập này yêu cầu sinh viên thiết kế và triển khai một hệ thống phân tán kết nối với Facebook Graph API, xử lý sự kiện theo thời gian thực, truyền dữ liệu qua Kafka và phân tích cảm xúc bằng AI API[cite: 5].
* [cite_start]**Mục tiêu:** Không chỉ là gọi được API, mà còn là hiểu cách nhiều service nhỏ phối hợp với nhau trong một hệ thống thực tế, thấy được luồng dữ liệu đi từ Facebook, qua các service xử lý, rồi quay trở lại hệ thống để phản hồi hoặc lưu trữ[cite: 6].

---

## [cite_start]2. Kiến trúc hệ thống [cite: 8]

### [cite_start]2.1 Sơ đồ luồng dữ liệu (Kiến trúc) [cite: 9]
1. [cite_start]**Facebook Page** gửi sự kiện qua `HTTP POST` đến **Webhook + Processing**[cite: 10, 11, 12].
2. [cite_start]**Webhook + Processing** (`webhook-service`, port: 3001) tiến hành parse và normalize, sau đó publish vào topic `raw_events` trên **Kafka Broker**[cite: 12, 13, 14, 15, 16, 17].
3. [cite_start]**Core Service** (`core-service`, port: 3002) consume từ `raw_events`, thực hiện phân tích AI + Automation, rồi publish vào topic `reply_commands`[cite: 18, 19, 20, 21, 22, 23, 24, 25].
4. [cite_start]**Backend API** (`backend-api`, port: 3000) consume từ `reply_commands` và `send_retry`[cite: 29, 30, 33, 36].
   * [cite_start]Gửi phản hồi hoặc đăng bài[cite: 31, 32].
   * [cite_start]Thực hiện Check/Save Idempotency key vào **Database**[cite: 34, 35, 37, 38].
   * [cite_start]Nếu gửi thất bại, publish vào topic `send_failed` trên Kafka[cite: 39, 40, 41].
5. [cite_start]**Retry Service** (`retry-service`, port: 3003) consume từ `send_failed`, áp dụng logic exponential backoff[cite: 42, 43, 44, 45, 46].
   * [cite_start]Nếu số lần thử (counter) < N: publish lại vào topic `send_retry`[cite: 26, 27, 28].
   * [cite_start]Nếu số lần thử (counter) >= N: publish vào Kafka topic `dead_letter`[cite: 47, 48, 49].
6. [cite_start]**Prometheus** theo dõi offset từ `dead_letter` và **Alertmanager** bắn cảnh báo qua Slack[cite: 50, 51, 52].

### [cite_start]2.2 Mô tả các service [cite: 54]
* [cite_start]**Facebook Page:** Khi có người dùng bình luận hoặc nhắn tin, Facebook gửi HTTP POST đến Webhook endpoint của hệ thống[cite: 55].
* [cite_start]**Webhook + Processing (`webhook-service`, port 3001):** Nhận event từ Facebook[cite: 56]. [cite_start]Xác thực chữ ký HMAC-SHA256 để đảm bảo request đến từ Facebook thật, parse payload JSON, normalize về schema chuẩn nội bộ, rồi publish vào topic `raw_events`[cite: 57]. [cite_start]Phải trả `200 OK` cho Facebook càng nhanh càng tốt để tránh bị retry[cite: 58].
* [cite_start]**Kafka Broker:** Hạ tầng truyền thông điệp trung tâm[cite: 59]. [cite_start]Mọi giao tiếp nội bộ đều đi qua Kafka với các topic: [cite: 60]
  * [cite_start]`raw_events`: Webhook + Processing publish, Core Service consume[cite: 61].
  * [cite_start]`reply_commands`: Core Service publish, Backend API consume[cite: 61].
  * [cite_start]`send_retry`: Retry Service publish, Backend API consume[cite: 62].
  * [cite_start]`send_failed`: Backend API publish, Retry Service consume[cite: 63].
  * [cite_start]`dead_letter`: Retry Service publish khi hết số lần thử (Không có consumer)[cite: 64]. [cite_start]Prometheus theo dõi offset để phát hiện message mới và Alertmanager bắn cảnh báo[cite: 65].
* [cite_start]**Core Service (`core-service`, port 3002):** Consume topic `raw_events` và xử lý theo 2 bước: [cite: 66]
  * [cite_start]*Bước AI:* Gọi LLM (OpenAI, Gemini, Claude, v.v.) để phân loại intent (hỏi giá, khiếu nại, spam...) và phân tích sentiment (tích cực, trung tính, tiêu cực)[cite: 67, 68].
  * [cite_start]*Bước Automation:* Áp dụng rule engine để tự động reply, ẩn bình luận, hoặc đưa vào hàng chờ[cite: 69]. [cite_start]Với trường hợp tái phạm, ưu tiên block thủ công hoặc qua API nếu đã kiểm chứng[cite: 70, 71]. [cite_start]Kết quả publish vào `reply_commands`[cite: 72].
* [cite_start]**Backend API (`backend-api`, port 3000):** Consume `reply_commands` và `send_retry`[cite: 73]. [cite_start]Là service duy nhất gọi Facebook Graph API[cite: 74]. [cite_start]Kiểm tra idempotency key trong Database trước khi gửi để tránh trùng lặp[cite: 75]. [cite_start]Nếu thành công thì lưu key, thất bại thì publish vào `send_failed`[cite: 76]. [cite_start]Service này expose REST API cho dashboard quản trị[cite: 77].
* [cite_start]**Database:** Lưu idempotency key của từng `command_id` đã xử lý[cite: 78].
* [cite_start]**Retry Service (`retry-service`, port 3003):** Consume `send_failed`[cite: 79]. Đọc retry_count, tính thời gian chờ exponential backoff[cite: 80]. Nếu chưa đến ngưỡng N, publish vào `send_retry`[cite: 81]. [cite_start]Nếu đạt ngưỡng, publish vào `dead_letter` và dừng retry[cite: 82].
* [cite_start]**Dead Letter Queue (`dead_letter` topic):** Lưu toàn bộ message thất bại sau khi đã retry hết số lần[cite: 84, 85]. [cite_start]Prometheus theo dõi offset topic này và Alertmanager gửi cảnh báo đến Slack/Email để admin xử lý thủ công qua Kafka UI[cite: 86, 87].

### [cite_start]2.3 Quy ước đặt tên service và port triển khai [cite: 88]
* `backend-api`: Port 3000 (REST API + Facebook Graph API)[cite: 89].
* [cite_start]`webhook-service`: Port 3001 (Nhận webhook, verify chữ ký, đẩy vào Kafka)[cite: 90].
* [cite_start]`core-service`: Port 3002 (AI, sentiment, automation rule)[cite: 91].
* `retry-service`: Port 3003 (Retry logic)[cite: 92].

### 2.4 Giao tiếp giữa các service [cite: 93]
* [cite_start]Mọi giao tiếp nội bộ qua Kafka, không gọi HTTP trực tiếp[cite: 94, 95].
* [cite_start]Chỉ Backend API được gọi Facebook Graph API[cite: 96, 97].
* Idempotency bắt buộc cho mọi consumer[cite: 98, 99].
* [cite_start]Retry có giới hạn tối đa N lần với exponential backoff, vượt quá đưa vào `dead_letter`[cite: 100, 101].

### [cite_start]2.5 Sinh viên cần thực hiện những bước gì? [cite: 102]
1. [cite_start]Tạo Facebook App, Facebook Page và cấu hình Graph API, Webhooks[cite: 103].
2. [cite_start]Xây dựng Backend API làm proxy cho Facebook Graph API[cite: 104].
3. [cite_start]Cài đặt Webhook + Processing Endpoint, xác thực HMAC-SHA256[cite: 105].
4. [cite_start]Thiết kế Kafka topic, producer, consumer[cite: 106].
5. [cite_start]Xây dựng Core Service (AI + Automation)[cite: 108].
6. [cite_start]Xây dựng cơ chế xử lý lỗi hoàn chỉnh (Retry, DLQ, Prometheus + Alertmanager)[cite: 109].
7. [cite_start]Đảm bảo tính idempotent bằng Database[cite: 110].
8. [cite_start]Kiểm thử toàn bộ luồng[cite: 111].
9. [cite_start]Kiểm thử các kịch bản lỗi (retry hết lần, đẩy vào DLQ, kích hoạt cảnh báo)[cite: 112].

---

## 3. Các bài tập chi tiết

### [cite_start]Bài 1: Tích hợp Facebook API và xây dựng Backend [cite: 117, 118]
* [cite_start]**Mục tiêu:** Tích hợp Facebook Graph API và xây dựng dịch vụ backend trung gian[cite: 119].
* **Yêu cầu:**
  * [cite_start]Tạo Page và App, lấy Page Access Token[cite: 121, 122, 123, 124].
  * [cite_start]Cài đặt API: `GET /posts`, `POST /post`, `GET /comments`[cite: 125, 126, 127, 128].
  * Backend là proxy, thiết kế phân quyền cho dashboard[cite: 129, 130, 131, 132, 133].
  * [cite_start]Chuẩn hóa response, mã lỗi (ví dụ: 401), ghi log đầy đủ request/response[cite: 135, 136, 137, 138].
  * [cite_start]Xử lý lỗi phía Facebook API (VD: lỗi 500) theo hướng giám sát và khôi phục[cite: 139, 140].

### [cite_start]Bài 2: Xử lý thời gian thực với Webhook và Kafka [cite: 145, 146]
* [cite_start]**Mục tiêu:** Xây dựng hệ thống hướng sự kiện theo thời gian thực[cite: 147].
* **Yêu cầu:**
  * [cite_start]`webhook-service` nhận webhook, xác thực `X-Hub-Signature-256`, parse và đẩy vào `raw_events`[cite: 149, 150, 151, 152, 153, 154].
  * Normalize event về 1 schema chuẩn cho cả message và comment[cite: 157, 158].
  * [cite_start]`core-service` consume `raw_events` và xử lý: Phát hiện spam, dùng AI xác định intent/sentiment, và ra quyết định tự động[cite: 159, 161, 162, 163, 164, 165, 166, 167, 168, 169].
  * Thiết kế consumer chịu tải đột biến, theo dõi trạng thái xử lý sự kiện, ghi nhận thất bại để đẩy sang Retry Service[cite: 170, 171, 172, 173, 174, 175].
* [cite_start]**Logic xử lý lỗi:** * Rate limiting: Quá nhiều bình luận sẽ đánh dấu pending_review[cite: 177, 178, 179, 180].
  * Retry tối đa N lần với exponential backoff, sau đó đẩy sang `dead_letter`[cite: 181, 182, 183].
  * [cite_start]Circuit breaker khi downstream lỗi liên tiếp (VD: 10 request lỗi -> ngắt gọi tạm thời)[cite: 184, 185].
  * [cite_start]Idempotent check trong DB để không xử lý trùng sự kiện[cite: 187, 188].
  * DLQ kích hoạt Prometheus alert và Slack notification[cite: 189, 190].

### Bài 3: Phân tích cảm xúc bằng AI và tự động hóa [cite: 196, 197]
* [cite_start]**Mục tiêu:** Nhận bình luận, AI phân loại, tự động hóa phản hồi[cite: 198, 199, 200, 219, 220].
* [cite_start]**Yêu cầu:** * Phân loại: Tích cực, Trung tính, Tiêu cực[cite: 202, 203, 204, 205, 206, 207].
  * Luật tự động: Tích cực -> Cảm ơn; Tiêu cực -> Xin lỗi; [cite_start]Spam -> Ẩn bình luận[cite: 208, 209, 210, 212, 213, 214, 215].
* [cite_start]**YÊU CẦU BẮT BUỘC CHẤM ĐIỂM:** [cite: 221, 232, 234, 235]
  1. [cite_start]Retry với exponential backoff (Phân biệt lỗi tạm thời và không thể khôi phục)[cite: 222, 223, 236, 237].
  2. [cite_start]Circuit breaker có điều kiện đóng/mở rõ ràng[cite: 224, 225, 226, 238, 239].
  3. [cite_start]Kafka consumer có tính idempotent lưu Database (command_id)[cite: 227, 228, 229, 240, 241].
  4. [cite_start]Dead Letter Queue lưu message thất bại và cảnh báo Prometheus/Alertmanager (dưới 1 phút)[cite: 230, 231, 242, 243].

---

## [cite_start]Phụ lục: Cấu hình Môi trường Phát triển (Docker) [cite: 253]

### [cite_start]Cấu trúc thư mục [cite: 260]
```text
fb_api/
  docker-compose.yml
  prometheus/
    prometheus.yml
    alert.rules.yml
  alertmanager/
    alertmanager.yml
  services/
    backend-api/
    webhook-service/
    core-service/
    retry-service/
```

---

## 4. Checklist Các Công Việc Cần Thực Hiện (Implementation Checklist)

Đây là danh sách các tính năng còn thiếu so với yêu cầu, sẽ được thực hiện và đánh dấu dần:

### 4.1. Cấu hình Database & Entity Framework (Backend API)
- [x] Khởi tạo Entity Framework Core (Sử dụng SQLite).
- [x] Thêm bảng lưu `processed_commands` để phục vụ Idempotency.

### 4.2. Core Service (Trí tuệ nhân tạo & Tự động hóa)
- [x] Khởi tạo Kafka Consumer để lắng nghe topic `raw_events`.
- [x] Tích hợp API AI (Sử dụng Mock Service) để phân loại Ý định (Intent) và Cảm xúc (Sentiment).
- [x] Xây dựng Rule Engine ra quyết định (Reply, Hide, Blacklist) dựa trên kết quả AI.
- [x] Tạo Kafka Producer để đẩy lệnh xử lý vào topic `reply_commands`.

### 4.3. Backend API (Xử lý Idempotency & Gửi Facebook)
- [x] Sửa lại Kafka Consumer hiện tại: Lắng nghe `reply_commands` và `send_retry` (thay vì đọc thẳng raw event).
- [x] Implement logic Idempotency: Cập nhật `FacebookEventHandler` để kiểm tra Database trước khi gọi Facebook API.
- [x] Tích hợp thư viện **Polly** để làm **Circuit Breaker** khi gọi Facebook Graph API.
- [x] Xử lý khi gọi Facebook lỗi: Tự động publish message vào topic `send_failed` kèm theo `retry_count`.

### 4.4. Retry Service (Cơ chế thử lại & Dead Letter Queue)
- [x] Khởi tạo Kafka Consumer để lắng nghe topic `send_failed`.
- [x] Áp dụng logic delay **Exponential Backoff**.
- [x] Logic điều phối (Routing):
  - [x] Nếu `retry_count < N`: Tăng đếm và publish lại vào topic `send_retry`.
  - [x] Nếu `retry_count >= N`: Dừng thử lại và publish vào topic `dead_letter`.

### 4.5. Hạ tầng Giám sát (Monitoring)
- [x] Thêm cấu hình Prometheus để theo dõi luồng Kafka (đặc biệt là DLQ).
- [x] Thêm Alertmanager để bắn cảnh báo lỗi không thể khôi phục.