# Hướng dẫn sử dụng SniffCom

## 1. Tổng quan project

SniffCom là ứng dụng WPF chạy trên Windows để:

- Đọc dữ liệu từ COM và hiển thị dạng text hoặc HEX.
- Gửi dữ liệu sang COM khác, có thể tự động gửi lại dữ liệu vừa nhận.
- Chạy bài kiểm tra áp suất khí (Air Press Test/APT) qua COM.
- Kích hoạt bài test tự động bằng nội dung COM hoặc bằng ảnh xuất hiện trên màn hình.
- Điều khiển relay sau khi test đạt, hỗ trợ relay USB dùng DLL BITFT và relay nối tiếp CH340.
- Lưu cấu hình người dùng vào `%APPDATA%\SniffCom\appSettings.json`.
- Lưu log vào thư mục `logs` dưới thư mục chạy chương trình.

## 2. Cấu trúc file chính

- `MainWindow.xaml`: giao diện chính.
- `MainViewModel.cs`: khởi tạo cấu hình, COM đọc/gửi, log, đóng tài nguyên khi thoát.
- `MainVM_AppSettings.cs`: nạp và lưu cấu hình tự động.
- `MainVM_HexView.cs`: log text/HEX và kích hoạt theo nội dung COM.
- `MainVM_AirPressTest.cs`: kết nối APT COM, gửi lệnh `:TEST`, đọc `:PRESS`, `:WAIT`, `:RESULT`.
- `MainVM_TriggerTest.cs`: cấu hình kích hoạt theo ảnh, ROI, timer quét ảnh.
- `MainVM_RelayControl.cs`: điều khiển relay BITFT hoặc CH340.
- `ulti\ImageMatcher.cs`: tải ảnh mẫu và dùng OpenCV để tìm ảnh trên màn hình.
- `ulti\ScreenCapture.cs`: chụp toàn màn hình hoặc vùng ROI.
- `usb_relay_device.dll`: thư viện native cho relay BITFT.

## 3. Cách chạy cơ bản

1. Mở ứng dụng.
2. Chọn COM đọc dữ liệu, baudrate, bấm kết nối COM nếu cần đọc dữ liệu.
3. Chọn COM gửi dữ liệu nếu cần gửi thủ công hoặc bật `Tự động gửi`.
4. Trong phần APT, chọn COM của máy test áp suất, baudrate, bật APT và kết nối.
5. Chọn kênh test cần dùng, chỉnh ngưỡng áp suất, thời gian tăng/giữ áp.
6. Chọn loại relay và kiểm tra relay trước khi chạy sản xuất.

## 4. Cấu hình kích hoạt tự động bằng nội dung COM

1. Trong phần `KÍCH HOẠT TỰ ĐỘNG`, chọn `Theo nội dung nhận từ COM`.
2. Nhập chuỗi cần khớp vào `Nội dung cần khớp`.
3. Khi dữ liệu COM nhận được có chứa chuỗi này, ứng dụng gọi `StartAirPressTestAsync()` để bắt đầu APT.

Lưu ý: so khớp không phân biệt chữ hoa/thường và có xử lý trường hợp chuỗi trigger bị chia giữa hai lần nhận COM.

## 5. Cấu hình quét ảnh tự động

### 5.1. Ảnh mẫu phải đặt ở đâu

Đường dẫn mặc định của ảnh mẫu là:

```text
trigger\img\template.png
```

Đường dẫn này được tính từ thư mục chạy của chương trình, tức là cùng cấp với file `.exe`.

Ví dụ khi chạy bản build Debug:

```text
bin\x86\Debug\net10.0-windows\trigger\img\template.png
```

Ví dụ khi chạy bản Release:

```text
bin\x86\Release\net10.0-windows\trigger\img\template.png
```

Nếu đặt ảnh trong source project để build tự copy, đặt tại:

```text
SniffCom\trigger\img\template.png
```

File `.csproj` đã khai báo copy các file `*.png` trong `trigger\img` sang thư mục chạy khi build/publish.

### 5.2. Ảnh mẫu nên chụp như thế nào

Ảnh mẫu nên là ảnh PNG nhỏ, rõ, đúng phần sẽ xuất hiện trên màn hình khi cần kích hoạt.

Khuyến nghị:

- Tên file bắt buộc dùng mặc định: `template.png`.
- Chụp đúng biểu tượng, chữ, nút, nhãn hoặc vùng hình ảnh đặc trưng cần nhận diện.
- Không chụp vùng quá lớn; ảnh mẫu càng gọn càng ổn định.
- Không chụp vùng có đồng hồ, số thay đổi, con trỏ chuột, animation hoặc nội dung thay đổi liên tục.
- Ảnh mẫu phải có kích thước nhỏ hơn vùng ROI.
- Nên giữ cùng tỉ lệ scale Windows/DPI, cùng độ phân giải màn hình và cùng giao diện phần mềm cần quét như lúc chụp mẫu.
- Nếu phần cần nhận diện có thể đổi màu hoặc đổi theme, hãy chụp lại `template.png` theo đúng trạng thái sử dụng thực tế.

Ví dụ mẫu tốt:

```text
Một icon PASS cố định
Một chữ "READY" cố định
Một nút hoặc nhãn trạng thái có màu và hình dạng ổn định
```

Ví dụ mẫu không nên dùng:

```text
Cả cửa sổ phần mềm lớn
Vùng có số serial thay đổi
Vùng có thời gian chạy
Vùng có nền động hoặc hình ảnh bị mờ
```

### 5.3. Cách bật quét ảnh để tự động chạy APT

1. Đặt file ảnh mẫu vào đúng đường dẫn `trigger\img\template.png`.
2. Mở lại ứng dụng để ảnh mẫu được tải, hoặc đảm bảo ảnh đã có trước khi chạy.
3. Mở màn hình/phần mềm bên ngoài có chứa hình ảnh cần nhận diện.
4. Trong SniffCom, chọn `Theo hình ảnh trên màn hình`.
5. Bấm `Quét ảnh mẫu`.
   - Ứng dụng sẽ nạp lại `template.png`, tìm ảnh mẫu trên toàn màn hình, tự cập nhật ROI và bật quét ảnh tự động.
   - Nếu tìm thấy, chương trình tự cập nhật ROI gồm `X`, `Y`, `Rộng`, `Cao` nội bộ.
6. Bấm `Kiểm tra ảnh`.
   - Nếu status báo `Kiểm tra ảnh ĐẠT`, ảnh mẫu và ROI đang đúng.
   - Nếu báo không đạt, kiểm tra lại vị trí ảnh, kích thước ROI, DPI hoặc chụp lại ảnh mẫu.
7. Nhập `Chu kỳ quét (ms)`.
   - Giá trị nhỏ nhất trong code là `300 ms`.
   - Khuyến nghị dùng `500 ms` đến `1000 ms` để giảm tải CPU.
8. Tick `Bật quét hình ảnh`.
9. Khi ảnh mẫu xuất hiện trong ROI với độ tương đồng từ `85%` trở lên, ứng dụng tự chạy APT.

### 5.4. Điều kiện để quét ảnh thật sự chạy

Timer quét ảnh chỉ chạy khi đồng thời thỏa các điều kiện:

- Đã chọn `Theo hình ảnh trên màn hình`.
- Đã tick `Bật quét hình ảnh`.
- Ứng dụng đã tải được `trigger\img\template.png`.
- APT đang được bật.
- APT COM đã kết nối.
- Đã chọn ít nhất một kênh test.
- Không có bài test nào đang chạy.

Khi timer tìm thấy ảnh, nó tạm dừng quét, chạy APT, sau đó tự bật lại nếu cấu hình quét ảnh vẫn còn bật.

### 5.5. Lỗi thường gặp khi quét ảnh

- `Không tìm thấy ảnh mẫu`: thiếu file `template.png` hoặc đặt sai thư mục.
- `Ảnh mẫu chưa được tải`: app mở trước khi có ảnh, ảnh lỗi, hoặc không phải PNG hợp lệ.
- `Vùng quét ROI quá nhỏ so với ảnh mẫu`: ROI nhỏ hơn kích thước `template.png`; bấm lại `Chọn vùng quét` hoặc dùng ảnh mẫu nhỏ hơn.
- `Không tìm thấy ảnh mẫu ... < 85%`: ảnh trên màn hình khác ảnh mẫu do scale, font, màu, độ phân giải, theme hoặc vùng bị che.
- Không tự chạy APT dù đã thấy ảnh: kiểm tra APT COM đã kết nối, `Theo hình ảnh trên màn hình` và `Bật quét hình ảnh` đều đã bật.

## 6. Relay sau khi test

Sau khi nhận `:RESULT`, chương trình tính kết quả từng kênh:

- Kênh tắt sẽ được xem là đạt và hiển thị `---`.
- Kênh bật đạt khi áp suất đầu vào đủ ngưỡng và độ rò không vượt ngưỡng.
- Nếu tất cả kênh đang bật đều đạt và `RelayOutputHoldTime > 100`, chương trình bật relay theo thời gian cấu hình.

Relay BITFT dùng `usb_relay_device.dll` và serial relay mục tiêu trong code là `BITFT`.
Relay CH340 dùng lệnh HEX mặc định:

```text
ON : A0 01 01 A2
OFF: A0 01 00 A1
```

## 7. Các thay đổi đã thực hiện trong lần rà soát này

- Thêm file hướng dẫn sử dụng `HUONG_DAN_SU_DUNG.md`.
- Thêm thư mục hướng dẫn vị trí ảnh mẫu `trigger\img`.
- Cập nhật SniffCom.csproj để copy các file PNG trong 	rigger\img sang thư mục chạy khi build/publish.
- Đổi nút Chọn vùng quét thành Quét ảnh mẫu; khi bấm sẽ nạp ảnh mẫu, tìm vị trí ảnh trên màn hình, cập nhật ROI và bật quét ảnh tự động.

Chỉ thay đổi nhỏ ở luồng nút quét ảnh mẫu và nội dung hiển thị của nút; không sửa logic COM, APT hoặc relay.

