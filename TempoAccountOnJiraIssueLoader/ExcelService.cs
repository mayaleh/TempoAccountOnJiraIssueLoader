using Maya.Ext;
using Maya.Ext.Rop;
using NPOI.SS.Formula.Functions;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.XWPF.UserModel;
using System.IO;

namespace TempoAccountOnJiraIssueLoader
{
    internal class ExcelService
    {
        const int IssueCellNum = 0;
        const int AccountKeyCellNum = 9;
        const int StartReadRowNum = 1;

        public Task<List<Result<string, (Exception Exception, int RowNr)>>> ReadIIssuesKeysFileAsync(
            FileStream fileStream, Action<int>? onProgressChanged = null)
        {
            try
            {
                fileStream.Position = 0;

                var xssWorkbook = new XSSFWorkbook(fileStream);
                var sheet = xssWorkbook.GetSheetAt(0);

                var worklogsResults = new List<Result<string, (Exception, int)>>();

                for (var i = StartReadRowNum; i <= sheet.LastRowNum; i++)
                {
                    var row = sheet.GetRow(i);

                    if (row == null) continue; // empty row

                    if (row.Cells.All(d => d.CellType == CellType.Blank)) continue; // all cells in row are empty
                    var cellVal = row.GetCell(IssueCellNum).StringCellValue;
                    worklogsResults.Add(Result<string, (Exception Exception, int RowNr)>.Succeeded(cellVal)
                                        ?? Result<string, (Exception Exception, int RowNr)>
                                            .Failed((new Exception(), i)));

                    onProgressChanged?.Invoke(i);
                }

                return Task.FromResult(worklogsResults);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public Task<Result<Unit, Exception>> FillAccountToExcel(string filePath,
            Dictionary<string, AccountResponse> issueAccount)
        {
            try
            {
                //fileStream.Position = 0;
                XSSFWorkbook xssWorkbook;
                using (var readStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    xssWorkbook = new XSSFWorkbook(readStream);
                    readStream.Close();
                }

                var sheet = xssWorkbook.GetSheetAt(0);

                for (var i = StartReadRowNum; i <= sheet.LastRowNum; i++)
                {
                    var row = sheet.GetRow(i);

                    if (row == null) continue; // empty row

                    if (row.Cells.All(d => d.CellType == CellType.Blank)) continue; // all cells in row are empty

                    var issueKey = row.GetCell(IssueCellNum).StringCellValue;

                    if (string.IsNullOrWhiteSpace(issueKey))
                    {
                        continue;
                    }

                    if (!issueAccount.TryGetValue(issueKey, out var accountKey))
                    {
                        continue;
                    }

                    var accountCell = row.CreateCell(AccountKeyCellNum, CellType.String);
                    accountCell.SetCellValue(accountKey.Key);
                }

                using var writeStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                xssWorkbook.Write(writeStream);
                writeStream.Close();

                return Task.FromResult(Result<Unit, Exception>.Succeeded(Unit.Default));
            }
            catch (Exception e)
            {
                return Task.FromResult(Result<Unit, Exception>.Failed(e));
            }
        }
    }
}