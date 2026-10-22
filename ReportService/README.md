# ReportService

Service FastAPI port 5104 tổng hợp cảnh báo đã lưu và xuất Excel. API đi qua Gateway tại `/reportservice/statistics` và `/reportservice/statistics/export`; yêu cầu `Alert.Read` hoặc `Alert.Manage`.

Service chỉ đọc `alert.alerts` trong PostgreSQL, không tạo hay sửa bảng cảnh báo. Ngày báo cáo là UTC, mặc định 30 ngày, tối đa 366 ngày; Excel giới hạn 10.000 cảnh báo mỗi lần tải. Chi tiết luồng và cách tự kiểm tra ở [bài học Phase 3](../docs/learn/phase-3-detectors-reports.md).
