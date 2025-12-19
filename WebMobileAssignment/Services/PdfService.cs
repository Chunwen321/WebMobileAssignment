using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WebMobileAssignment.Models;

namespace WebMobileAssignment.Services
{
    public class PdfService
    {
        public byte[] GenerateAttendanceHistoryPdf(List<AttendanceReportItem> attendances, string studentName, string studentId)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    // Header
                    page.Header().Element(container =>
                    {
                        container.Column(column =>
                        {
                            column.Item().PaddingBottom(10).Row(row =>
                            {
                                row.RelativeItem().Column(col =>
                                {
                                    col.Item().Text("Attendance History Report")
                                        .FontSize(20)
                                        .Bold()
                                        .FontColor(Colors.Green.Darken2);
                                    
                                    col.Item().PaddingTop(5).Text($"Student: {studentName}")
                                        .FontSize(12)
                                        .FontColor(Colors.Grey.Darken2);
                                    
                                    col.Item().Text($"Student ID: {studentId}")
                                        .FontSize(12)
                                        .FontColor(Colors.Grey.Darken2);
                                });

                                row.ConstantItem(120).AlignRight().Column(col =>
                                {
                                    col.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd}")
                                        .FontSize(10)
                                        .FontColor(Colors.Grey.Medium);
                                    
                                    col.Item().Text($"Time: {DateTime.Now:HH:mm}")
                                        .FontSize(10)
                                        .FontColor(Colors.Grey.Medium);
                                });
                            });

                            column.Item().PaddingTop(5).LineHorizontal(2).LineColor(Colors.Green.Darken2);
                        });
                    });

                    // Content - Summary Statistics
                    page.Content().Column(column =>
                    {
                        // Summary Section
                        column.Item().PaddingTop(15).PaddingBottom(15).Row(row =>
                        {
                            var presentCount = attendances.Count(a => a.Status == "Present");
                            var absentCount = attendances.Count(a => a.Status == "Absent");
                            var leaveCount = attendances.Count(a => a.Status == "Leave");

                            // Present Box
                            row.RelativeItem().Padding(5).Background(Colors.Green.Lighten3).Border(1).BorderColor(Colors.Green.Medium).Column(col =>
                            {
                                col.Item().AlignCenter().Text("Present").Bold().FontSize(11);
                                col.Item().AlignCenter().Text(presentCount.ToString()).FontSize(24).Bold().FontColor(Colors.Green.Darken2);
                            });

                            // Absent Box
                            row.RelativeItem().Padding(5).Background(Colors.Red.Lighten3).Border(1).BorderColor(Colors.Red.Medium).Column(col =>
                            {
                                col.Item().AlignCenter().Text("Absent").Bold().FontSize(11);
                                col.Item().AlignCenter().Text(absentCount.ToString()).FontSize(24).Bold().FontColor(Colors.Red.Darken2);
                            });

                            // Leave Box
                            row.RelativeItem().Padding(5).Background(Colors.Blue.Lighten3).Border(1).BorderColor(Colors.Blue.Medium).Column(col =>
                            {
                                col.Item().AlignCenter().Text("Leave").Bold().FontSize(11);
                                col.Item().AlignCenter().Text(leaveCount.ToString()).FontSize(24).Bold().FontColor(Colors.Blue.Darken2);
                            });
                        });

                        // Attendance Table
                        column.Item().PaddingTop(10).Table(table =>
                        {
                            // Define columns
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(80);  // Date
                                columns.ConstantColumn(80);  // Day
                                columns.RelativeColumn(1.5f); // Class
                                columns.RelativeColumn(2);   // Subject
                                columns.ConstantColumn(70);  // Time
                                columns.ConstantColumn(70);  // Status
                            });

                            // Header
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Grey.Darken2).Padding(5).Text("Date").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Grey.Darken2).Padding(5).Text("Day").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Grey.Darken2).Padding(5).Text("Class").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Grey.Darken2).Padding(5).Text("Subject").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Grey.Darken2).Padding(5).Text("Time").FontColor(Colors.White).Bold();
                                header.Cell().Background(Colors.Grey.Darken2).Padding(5).Text("Status").FontColor(Colors.White).Bold();
                            });

                            // Data rows
                            foreach (var attendance in attendances)
                            {
                                var backgroundColor = attendance.Status switch
                                {
                                    "Present" => Colors.Green.Lighten4,
                                    "Absent" => Colors.Red.Lighten4,
                                    "Leave" => Colors.Blue.Lighten4,
                                    _ => Colors.Grey.Lighten4
                                };

                                var statusColor = attendance.Status switch
                                {
                                    "Present" => Colors.Green.Darken2,
                                    "Absent" => Colors.Red.Darken2,
                                    "Leave" => Colors.Blue.Darken2,
                                    _ => Colors.Grey.Darken2
                                };

                                table.Cell().Background(backgroundColor).Padding(5).Text(attendance.Date);
                                table.Cell().Background(backgroundColor).Padding(5).Text(attendance.Day);
                                table.Cell().Background(backgroundColor).Padding(5).Text(attendance.ClassName);
                                table.Cell().Background(backgroundColor).Padding(5).Text(attendance.SubjectName);
                                table.Cell().Background(backgroundColor).Padding(5).Text(attendance.TimeTaken);
                                table.Cell().Background(backgroundColor).Padding(5).Text(attendance.Status).Bold().FontColor(statusColor);
                            }
                        });
                    });

                    // Footer
                    page.Footer().AlignCenter().DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Medium)).Text(text =>
                    {
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
                });
            });

            return document.GeneratePdf();
        }
    }

    // DTO for attendance report items
    public class AttendanceReportItem
    {
        public string Date { get; set; } = string.Empty;
        public string Day { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public string TimeTaken { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
