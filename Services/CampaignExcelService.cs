using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OfficeOpenXml;
using Synctool.Data;
using Synctool.Models;

namespace Synctool.Services
{
    public class CampaignDetail
    {
        public string CampaignCode { get; set; } = string.Empty;
        public string ShortDescription { get; set; } = string.Empty;
        public string GeneralDescription { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string TargetSheetName { get; set; } = string.Empty;
        public bool HasTargetSheet => !string.IsNullOrEmpty(TargetSheetName);
        public decimal DiscountAmount { get; set; }
        public decimal DiscountNetAmount { get; set; }
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
    }

    public class CampaignProductDetail
    {
        public string Column1 { get; set; } = string.Empty; // Code
        public string Column2 { get; set; } = string.Empty; // Name
        public string Column3 { get; set; } = string.Empty; // Price/Info
    }

    public class CampaignExcelService
    {
        public static async Task<Dictionary<string, List<CampaignDetail>>> GetCampaignsForProductsAsync(List<string> productCodes)
        {
            var results = new Dictionary<string, List<CampaignDetail>>();
            foreach (var pc in productCodes)
            {
                if (!string.IsNullOrWhiteSpace(pc))
                {
                    results[pc.Trim().ToUpperInvariant()] = new List<CampaignDetail>();
                }
            }

            if (results.Count == 0) return results;

            await Task.Run(() =>
            {
                string debugLogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "CampaignDebug.txt");
                File.WriteAllText(debugLogPath, "Starting Campaign Check...\n");

                using var context = new AppDbContext();
                var lastFile = context.UploadedFiles
                    .Where(f => f.Category == "Oliz Kampanya")
                    .OrderByDescending(f => f.Id)
                    .FirstOrDefault();

                if (lastFile == null || lastFile.FileData == null || lastFile.FileData.Length == 0)
                {
                    File.AppendAllText(debugLogPath, "No file uploaded.\n");
                    return;
                }

                ExcelPackage.License.SetNonCommercialPersonal("Synctool");
                using var ms = new MemoryStream(lastFile.FileData);
                using var package = new ExcelPackage(ms);
                
                var sheetContents = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

                foreach (var worksheet in package.Workbook.Worksheets)
                {
                    int rowCount = worksheet.Dimension?.Rows ?? 0;
                    int colCount = worksheet.Dimension?.Columns ?? 20; 
                    
                    File.AppendAllText(debugLogPath, $"Sheet '{worksheet.Name}' has {rowCount} rows, {colCount} cols.\n");

                    for (int row = 1; row <= rowCount; row++)
                    {
                        string rowProductCode = worksheet.Cells[row, 3].Text?.Trim().ToUpperInvariant() ?? string.Empty;

                        var detail = new CampaignDetail
                        {
                            Brand = worksheet.Cells[row, 1].Text ?? string.Empty,
                            CampaignCode = worksheet.Cells[row, 11].Text ?? string.Empty,
                            ShortDescription = worksheet.Cells[row, 12].Text ?? string.Empty,
                            GeneralDescription = worksheet.Cells[row, 13].Text ?? string.Empty,
                            StartDate = worksheet.Cells[row, 7].Text ?? string.Empty,
                            EndDate = worksheet.Cells[row, 8].Text ?? string.Empty,
                        };

                        if (decimal.TryParse(worksheet.Cells[row, 5].Text, out decimal d1)) detail.DiscountAmount = d1;
                        if (decimal.TryParse(worksheet.Cells[row, 6].Text, out decimal d2)) detail.DiscountNetAmount = d2;

                        // 1. Auto-detect hyperlink in any column up to 20
                        for (int c = 1; c <= Math.Min(colCount, 20); c++)
                        {
                            var cell = worksheet.Cells[row, c];
                            string extractedSheet = ExtractSheetFromCell(cell);
                            if (!string.IsNullOrEmpty(extractedSheet))
                            {
                                detail.TargetSheetName = extractedSheet;
                                detail.ShortDescription = cell.Text ?? detail.ShortDescription; // Set ShortDescription to the link text if empty
                                File.AppendAllText(debugLogPath, $"  Row {row}: Found link to '{extractedSheet}' in Col {c} with Text '{cell.Text}'\n");
                                break;
                            }
                        }

                        bool campaignAddedForThisRow = false;

                        // 2. Direct match
                        if (!string.IsNullOrEmpty(rowProductCode) && results.ContainsKey(rowProductCode))
                        {
                            results[rowProductCode].Add(detail);
                            campaignAddedForThisRow = true;
                        }

                        // 3. Linked sheet match
                        if (!campaignAddedForThisRow && !string.IsNullOrEmpty(detail.TargetSheetName))
                        {
                            var targetSheet = package.Workbook.Worksheets[detail.TargetSheetName];
                            if (targetSheet != null)
                            {
                                if (!sheetContents.TryGetValue(detail.TargetSheetName, out var textPool))
                                {
                                    textPool = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                    int tsRows = targetSheet.Dimension?.Rows ?? 0;
                                    int tsCols = targetSheet.Dimension?.Columns ?? 0;

                                    var skuColumns = new List<int>();
                                    for (int c = 1; c <= tsCols; c++)
                                    {
                                        if (targetSheet.Cells[1, c].Text?.Trim().ToUpperInvariant() == "SKU" ||
                                            targetSheet.Cells[2, c].Text?.Trim().ToUpperInvariant() == "SKU")
                                        {
                                            skuColumns.Add(c);
                                        }
                                    }

                                    if (skuColumns.Any())
                                    {
                                        File.AppendAllText(debugLogPath, $"  Sheet '{detail.TargetSheetName}': Found SKU headers in columns {string.Join(",", skuColumns)}\n");
                                        foreach (var c in skuColumns)
                                        {
                                            for (int r = 1; r <= tsRows; r++)
                                            {
                                                string val = targetSheet.Cells[r, c].Text?.Trim().ToUpperInvariant() ?? string.Empty;
                                                if (!string.IsNullOrEmpty(val)) textPool.Add(val);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        for (int r = 1; r <= tsRows; r++)
                                        {
                                            for (int c = 1; c <= tsCols; c++)
                                            {
                                                string val = targetSheet.Cells[r, c].Text?.Trim().ToUpperInvariant() ?? string.Empty;
                                                if (!string.IsNullOrEmpty(val)) textPool.Add(val);
                                            }
                                        }
                                    }

                                    sheetContents[detail.TargetSheetName] = textPool;
                                }

                                foreach (var pc in results.Keys)
                                {
                                    if (textPool.Contains(pc))
                                    {
                                        File.AppendAllText(debugLogPath, $"  MATCH FOUND! Product {pc} found in sheet {detail.TargetSheetName}\n");
                                        if (!results[pc].Any(x => x.TargetSheetName == detail.TargetSheetName && 
                                                                  x.ShortDescription == detail.ShortDescription))
                                        {
                                            results[pc].Add(detail);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                File.AppendAllText(debugLogPath, $"  Row {row}: Target sheet '{detail.TargetSheetName}' NOT FOUND in workbook.\n");
                            }
                        }
                    }
                }
            });

            return results;
        }

        private static string ExtractSheetFromCell(ExcelRange cell)
        {
            var hl = cell.Hyperlink;
            if (hl != null)
            {
                string linkStr = Uri.UnescapeDataString(hl.OriginalString);
                string sheet = ExtractSheetFromLinkStr(linkStr);
                if (!string.IsNullOrEmpty(sheet)) return sheet;
            }

            string formula = cell.Formula;
            if (!string.IsNullOrEmpty(formula) && formula.IndexOf("HYPERLINK", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                int firstQuote = formula.IndexOf('"');
                if (firstQuote > -1)
                {
                    int secondQuote = formula.IndexOf('"', firstQuote + 1);
                    if (secondQuote > firstQuote)
                    {
                        string linkStr = formula.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
                        string sheet = ExtractSheetFromLinkStr(linkStr);
                        if (!string.IsNullOrEmpty(sheet)) return sheet;
                    }
                }
            }
            return string.Empty;
        }

        private static string ExtractSheetFromLinkStr(string linkStr)
        {
            if (linkStr.StartsWith("#")) linkStr = linkStr.Substring(1);
            int exclaimIdx = linkStr.IndexOf('!');
            if (exclaimIdx > 0)
            {
                return linkStr.Substring(0, exclaimIdx).Trim('\'');
            }
            return string.Empty;
        }

        public static async Task<List<CampaignProductDetail>> GetProductsFromSheetAsync(string sheetName)
        {
            var products = new List<CampaignProductDetail>();

            if (string.IsNullOrWhiteSpace(sheetName))
                return products;

            await Task.Run(() =>
            {
                using var context = new AppDbContext();
                var lastFile = context.UploadedFiles
                    .Where(f => f.Category == "Oliz Kampanya")
                    .OrderByDescending(f => f.Id)
                    .FirstOrDefault();

                if (lastFile == null || lastFile.FileData == null || lastFile.FileData.Length == 0)
                    return;

                ExcelPackage.License.SetNonCommercialPersonal("Synctool");
                using var ms = new MemoryStream(lastFile.FileData);
                using var package = new ExcelPackage(ms);
                
                var worksheet = package.Workbook.Worksheets[sheetName];
                if (worksheet == null) return;

                int rowCount = worksheet.Dimension?.Rows ?? 0;
                int colCount = worksheet.Dimension?.Columns ?? 0;

                var tableStartCols = new List<int>();
                for (int c = 1; c <= colCount; c++)
                {
                    if (worksheet.Cells[1, c].Text?.Trim().ToUpperInvariant() == "SKU" ||
                        worksheet.Cells[2, c].Text?.Trim().ToUpperInvariant() == "SKU")
                    {
                        tableStartCols.Add(c);
                    }
                }

                if (!tableStartCols.Any())
                {
                    tableStartCols.Add(1);
                }

                foreach (var startCol in tableStartCols)
                {
                    for (int row = 1; row <= rowCount; row++)
                    {
                        string col1 = worksheet.Cells[row, startCol].Text?.Trim() ?? string.Empty;
                        string col2 = worksheet.Cells[row, startCol + 1].Text?.Trim() ?? string.Empty;
                        string col3 = worksheet.Cells[row, startCol + 2].Text?.Trim() ?? string.Empty;

                        if (col1.ToUpperInvariant() == "SKU") continue;

                        if (string.IsNullOrWhiteSpace(col1) && string.IsNullOrWhiteSpace(col2) && string.IsNullOrWhiteSpace(col3))
                            continue;

                        products.Add(new CampaignProductDetail
                        {
                            Column1 = col1,
                            Column2 = col2,
                            Column3 = col3
                        });
                    }
                }
            });

            return products;
        }
    }
}
