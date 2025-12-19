using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WebMobileAssignment.Models;

namespace WebMobileAssignment.Services
{
    public class ReportService
    {
        public class AttendanceSummaryData
        {
            public string MonthName { get; set; } = string.Empty;
            public string StudentName { get; set; } = string.Empty;
            public int TotalClasses { get; set; }
            public int TotalPresent { get; set; }
            public int TotalAbsent { get; set; }
            public int TotalLate { get; set; }
            public decimal AttendanceRate { get; set; }
            public List<SubjectSummary> SubjectSummaries { get; set; } = new();
        }

        public class SubjectSummary
        {
            public string Subject { get; set; } = string.Empty;
            public int TotalClasses { get; set; }
            public int Present { get; set; }
            public int Absent { get; set; }
            public int Late { get; set; }
            public decimal AttendanceRate { get; set; }
        }

        public byte[] GeneratePdfReport(AttendanceSummaryData data)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header()
                        .Height(80)
                        .Background(Colors.Grey.Lighten3)
                        .Padding(20)
                        .Column(column =>
                        {
                            column.Item()
                                .Text("Monthly Attendance Summary Report")
                                .FontSize(20)
                                .Bold()
                                .FontColor(Colors.Blue.Darken2);

                            column.Item()
                                .PaddingTop(5)
                                .Text($"Generated on: {DateTime.Now:MMMM dd, yyyy hh:mm tt}")
                                .FontSize(9)
                                .FontColor(Colors.Grey.Darken1);
                        });

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(column =>
                        {
                            column.Spacing(15);

                            // Student and Period Information
                            column.Item().Column(col =>
                            {
                                col.Item()
                                    .Text($"Period: {data.MonthName}")
                                    .FontSize(11)
                                    .SemiBold();

                                if (!string.IsNullOrEmpty(data.StudentName))
                                {
                                    col.Item()
                                        .PaddingTop(3)
                                        .Text($"Student: {data.StudentName}")
                                        .FontSize(11)
                                        .SemiBold();
                                }
                            });

                            // Overall Summary Section
                            column.Item().Background(Colors.Blue.Lighten4).Padding(15).Column(summaryCol =>
                            {
                                summaryCol.Item()
                                    .Text("Overall Summary")
                                    .FontSize(14)
                                    .Bold()
                                    .FontColor(Colors.Blue.Darken2);

                                summaryCol.Item().PaddingTop(10).Row(row =>
                                {
                                    row.RelativeItem().Column(col =>
                                    {
                                        col.Item().Text($"Total Classes: {data.TotalClasses}").SemiBold();
                                        col.Item().PaddingTop(3).Text($"Present: {data.TotalPresent}").FontColor(Colors.Green.Darken1);
                                    });

                                    row.RelativeItem().Column(col =>
                                    {
                                        col.Item().Text($"Absent: {data.TotalAbsent}").FontColor(Colors.Red.Darken1);
                                        col.Item().PaddingTop(3).Text($"Leave: {data.TotalLate}").FontColor(Colors.Orange.Darken1);
                                    });

                                    row.RelativeItem().Column(col =>
                                    {
                                        col.Item().Text("Attendance Rate").SemiBold();
                                        col.Item().PaddingTop(3).Text($"{data.AttendanceRate}%")
                                            .FontSize(16)
                                            .Bold()
                                            .FontColor(data.AttendanceRate >= 90 ? Colors.Green.Darken2 :
                                                       data.AttendanceRate >= 80 ? Colors.Orange.Darken2 :
                                                       Colors.Red.Darken2);
                                    });
                                });
                            });

                            // Subject-wise Summary Table
                            if (data.SubjectSummaries.Any())
                            {
                                column.Item().Text("Subject-wise Summary").FontSize(14).Bold();

                                column.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(3);
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(2);
                                    });

                                    // Header
                                    table.Header(header =>
                                    {
                                        header.Cell().Element(CellStyle).Text("Subject").Bold();
                                        header.Cell().Element(CellStyle).Text("Total").Bold();
                                        header.Cell().Element(CellStyle).Text("Present").Bold();
                                        header.Cell().Element(CellStyle).Text("Absent").Bold();
                                        header.Cell().Element(CellStyle).Text("Leave").Bold();
                                        header.Cell().Element(CellStyle).Text("Rate %").Bold();

                                        static IContainer CellStyle(IContainer container) => container
                                            .Background(Colors.Blue.Darken2)
                                            .Padding(8)
                                            .AlignCenter()
                                            .DefaultTextStyle(x => x.FontColor(Colors.White).FontSize(10));
                                    });

                                    // Data rows
                                    foreach (var subject in data.SubjectSummaries)
                                    {
                                        table.Cell().Element(RowCellStyle).Text(subject.Subject);
                                        table.Cell().Element(RowCellStyle).Text(subject.TotalClasses.ToString());
                                        table.Cell().Element(RowCellStyle).Text(subject.Present.ToString()).FontColor(Colors.Green.Darken1);
                                        table.Cell().Element(RowCellStyle).Text(subject.Absent.ToString()).FontColor(Colors.Red.Darken1);
                                        table.Cell().Element(RowCellStyle).Text(subject.Late.ToString()).FontColor(Colors.Orange.Darken1);
                                        table.Cell().Element(RowCellStyle).Text($"{subject.AttendanceRate:F1}%")
                                            .Bold()
                                            .FontColor(subject.AttendanceRate >= 90 ? Colors.Green.Darken2 :
                                                       subject.AttendanceRate >= 80 ? Colors.Orange.Darken2 :
                                                       Colors.Red.Darken2);
                                    }

                                    // Total row
                                    table.Cell().Element(TotalCellStyle).Text("Total").Bold();
                                    table.Cell().Element(TotalCellStyle).Text(data.TotalClasses.ToString()).Bold();
                                    table.Cell().Element(TotalCellStyle).Text(data.TotalPresent.ToString()).Bold().FontColor(Colors.Green.Darken1);
                                    table.Cell().Element(TotalCellStyle).Text(data.TotalAbsent.ToString()).Bold().FontColor(Colors.Red.Darken1);
                                    table.Cell().Element(TotalCellStyle).Text(data.TotalLate.ToString()).Bold().FontColor(Colors.Orange.Darken1);
                                    table.Cell().Element(TotalCellStyle).Text($"{data.AttendanceRate:F1}%").Bold();

                                    static IContainer RowCellStyle(IContainer container) => container
                                        .Border(1)
                                        .BorderColor(Colors.Grey.Lighten2)
                                        .Padding(8)
                                        .AlignCenter();

                                    static IContainer TotalCellStyle(IContainer container) => container
                                        .Background(Colors.Grey.Lighten3)
                                        .Border(1)
                                        .BorderColor(Colors.Grey.Lighten1)
                                        .Padding(8)
                                        .AlignCenter();
                                });
                            }

                            // Performance Note
                            column.Item().PaddingTop(20).Background(Colors.Blue.Lighten5).Padding(10).Column(noteCol =>
                            {
                                noteCol.Item().Text("Performance Note:").FontSize(10).SemiBold();
                                noteCol.Item().PaddingTop(3).Text("• 90% and above: Excellent")
                                    .FontSize(9).FontColor(Colors.Green.Darken2);
                                noteCol.Item().Text("• 80-89%: Good")
                                    .FontSize(9).FontColor(Colors.Orange.Darken2);
                                noteCol.Item().Text("• Below 80%: Needs Attention")
                                    .FontSize(9).FontColor(Colors.Red.Darken2);
                            });
                        });

                    page.Footer()
                        .Height(30)
                        .AlignCenter()
                        .DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken1))
                        .Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                            x.Span(" of ");
                            x.TotalPages();
                        });
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateExcelReport(AttendanceSummaryData data)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Attendance Summary");

            // Set column widths
            worksheet.Column(1).Width = 25;
            worksheet.Column(2).Width = 12;
            worksheet.Column(3).Width = 12;
            worksheet.Column(4).Width = 12;
            worksheet.Column(5).Width = 12;
            worksheet.Column(6).Width = 15;

            int currentRow = 1;

            // Title
            var titleCell = worksheet.Cell(currentRow, 1);
            titleCell.Value = "Monthly Attendance Summary Report";
            titleCell.Style.Font.FontSize = 16;
            titleCell.Style.Font.Bold = true;
            titleCell.Style.Font.FontColor = XLColor.DarkBlue;
            worksheet.Range(currentRow, 1, currentRow, 6).Merge();
            currentRow += 2;

            // Report Info
            worksheet.Cell(currentRow, 1).Value = "Generated on:";
            worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
            worksheet.Cell(currentRow, 2).Value = DateTime.Now.ToString("MMMM dd, yyyy hh:mm tt");
            worksheet.Range(currentRow, 2, currentRow, 6).Merge();
            currentRow++;

            worksheet.Cell(currentRow, 1).Value = "Period:";
            worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
            worksheet.Cell(currentRow, 2).Value = data.MonthName;
            worksheet.Range(currentRow, 2, currentRow, 6).Merge();
            currentRow++;

            if (!string.IsNullOrEmpty(data.StudentName))
            {
                worksheet.Cell(currentRow, 1).Value = "Student:";
                worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 2).Value = data.StudentName;
                worksheet.Range(currentRow, 2, currentRow, 6).Merge();
                currentRow++;
            }

            currentRow++;

            // Overall Summary Section
            var summaryTitleCell = worksheet.Cell(currentRow, 1);
            summaryTitleCell.Value = "Overall Summary";
            summaryTitleCell.Style.Font.FontSize = 14;
            summaryTitleCell.Style.Font.Bold = true;
            summaryTitleCell.Style.Fill.BackgroundColor = XLColor.LightBlue;
            worksheet.Range(currentRow, 1, currentRow, 6).Merge();
            worksheet.Range(currentRow, 1, currentRow, 6).Style.Fill.BackgroundColor = XLColor.LightBlue;
            currentRow++;

            // Summary data
            worksheet.Cell(currentRow, 1).Value = "Total Classes:";
            worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
            worksheet.Cell(currentRow, 2).Value = data.TotalClasses;

            worksheet.Cell(currentRow, 3).Value = "Present:";
            worksheet.Cell(currentRow, 3).Style.Font.Bold = true;
            worksheet.Cell(currentRow, 4).Value = data.TotalPresent;
            worksheet.Cell(currentRow, 4).Style.Font.FontColor = XLColor.Green;
            currentRow++;

            worksheet.Cell(currentRow, 1).Value = "Absent:";
            worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
            worksheet.Cell(currentRow, 2).Value = data.TotalAbsent;
            worksheet.Cell(currentRow, 2).Style.Font.FontColor = XLColor.Red;

            worksheet.Cell(currentRow, 3).Value = "Leave:";
            worksheet.Cell(currentRow, 3).Style.Font.Bold = true;
            worksheet.Cell(currentRow, 4).Value = data.TotalLate;
            worksheet.Cell(currentRow, 4).Style.Font.FontColor = XLColor.Orange;
            currentRow++;

            worksheet.Cell(currentRow, 1).Value = "Attendance Rate:";
            worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
            worksheet.Cell(currentRow, 2).Value = $"{data.AttendanceRate}%";
            worksheet.Cell(currentRow, 2).Style.Font.Bold = true;
            worksheet.Cell(currentRow, 2).Style.Font.FontSize = 14;
            worksheet.Cell(currentRow, 2).Style.Font.FontColor =
                data.AttendanceRate >= 90 ? XLColor.Green :
                data.AttendanceRate >= 80 ? XLColor.Orange : XLColor.Red;
            currentRow += 2;

            // Subject-wise Summary Table
            if (data.SubjectSummaries.Any())
            {
                var subjectTitleCell = worksheet.Cell(currentRow, 1);
                subjectTitleCell.Value = "Subject-wise Summary";
                subjectTitleCell.Style.Font.FontSize = 14;
                subjectTitleCell.Style.Font.Bold = true;
                worksheet.Range(currentRow, 1, currentRow, 6).Merge();
                currentRow++;

                // Table headers
                var headerRow = currentRow;
                worksheet.Cell(headerRow, 1).Value = "Subject";
                worksheet.Cell(headerRow, 2).Value = "Total Classes";
                worksheet.Cell(headerRow, 3).Value = "Present";
                worksheet.Cell(headerRow, 4).Value = "Absent";
                worksheet.Cell(headerRow, 5).Value = "Leave";
                worksheet.Cell(headerRow, 6).Value = "Attendance Rate";

                var headerRange = worksheet.Range(headerRow, 1, headerRow, 6);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.DarkBlue;
                headerRange.Style.Font.FontColor = XLColor.White;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                currentRow++;

                // Table data
                foreach (var subject in data.SubjectSummaries)
                {
                    worksheet.Cell(currentRow, 1).Value = subject.Subject;
                    worksheet.Cell(currentRow, 2).Value = subject.TotalClasses;
                    worksheet.Cell(currentRow, 3).Value = subject.Present;
                    worksheet.Cell(currentRow, 3).Style.Font.FontColor = XLColor.Green;
                    worksheet.Cell(currentRow, 4).Value = subject.Absent;
                    worksheet.Cell(currentRow, 4).Style.Font.FontColor = XLColor.Red;
                    worksheet.Cell(currentRow, 5).Value = subject.Late;
                    worksheet.Cell(currentRow, 5).Style.Font.FontColor = XLColor.Orange;
                    worksheet.Cell(currentRow, 6).Value = $"{subject.AttendanceRate:F1}%";

                    // Apply borders
                    var dataRange = worksheet.Range(currentRow, 1, currentRow, 6);
                    dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                    currentRow++;
                }

                // Total row
                worksheet.Cell(currentRow, 1).Value = "Total";
                worksheet.Cell(currentRow, 2).Value = data.TotalClasses;
                worksheet.Cell(currentRow, 3).Value = data.TotalPresent;
                worksheet.Cell(currentRow, 4).Value = data.TotalAbsent;
                worksheet.Cell(currentRow, 5).Value = data.TotalLate;
                worksheet.Cell(currentRow, 6).Value = $"{data.AttendanceRate:F1}%";

                var totalRange = worksheet.Range(currentRow, 1, currentRow, 6);
                totalRange.Style.Font.Bold = true;
                totalRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                totalRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
                totalRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                currentRow += 2;
            }

            // Performance Note
            currentRow++;
            var noteCell = worksheet.Cell(currentRow, 1);
            noteCell.Value = "Performance Note:";
            noteCell.Style.Font.Bold = true;
            currentRow++;

            worksheet.Cell(currentRow, 1).Value = "• 90% and above: Excellent";
            worksheet.Cell(currentRow, 1).Style.Font.FontColor = XLColor.Green;
            currentRow++;

            worksheet.Cell(currentRow, 1).Value = "• 80-89%: Good";
            worksheet.Cell(currentRow, 1).Style.Font.FontColor = XLColor.Orange;
            currentRow++;

            worksheet.Cell(currentRow, 1).Value = "• Below 80%: Needs Attention";
            worksheet.Cell(currentRow, 1).Style.Font.FontColor = XLColor.Red;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }
}
