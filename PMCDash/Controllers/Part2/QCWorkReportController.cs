using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSwag.Annotations;
using PMCDash.Models;
using PMCDash.Models.Part2;

namespace PMCDash.Controllers.Part2
{
    [Route("api/[controller]")]
    [ApiController]
    public class QCWorkReportController : BaseApiController
    {
        ConnectStr _ConnectStr = new ConnectStr();        //資料庫連線
        //private readonly string _ConnectStr.Local = @"Data Source = 192.168.0.156; Initial Catalog = DPI; User ID = MES2014; Password = PMCMES;";
        //private readonly string _ConnectStr.Local = @"Data Source = 127.0.0.1; Initial Catalog = DPI; User ID = MES2014; Password = PMCMES;";
        //時間格式
        private readonly string _timeFormat = "yyyy-MM-dd HH:mm:ss";
        private readonly string _dateFormat = "yyyy-MM-dd HH:mm:ss";

        private string checkNoword(string data)
        {
            if (data == "")
            {
                return "N/A";
            }
            else
            {
                return data;
            }
        }
        private int ChangeProgressIntFormat(string data)
        {
            if (data != "" && data != "N/A")
            {
                int n;
                if (int.TryParse(data, out n))
                {
                    if (n > 100) n = 100;
                    return n;
                }
                else
                {
                    return 0;
                }
            }
            return 0;
        }

        /// <summary>
        /// 取得檢驗工作任務清單列表
        /// </summary>
        /// <returns>IsQC:N/A(不需檢驗)、False(未檢驗、未檢驗完成)、True(已檢驗完成)</returns>
        [HttpGet("QCWorkTaskList")]
        public List<QCWorkTask> QCWorkTaskList(string Event)
        {
            string mod = "";
            //可檢驗(狀態為加工中/暫停/已完工)但還未檢驗
            if (Event == "0")
            {
                mod = "qcp.QCPointValue is null";
                //mod = "wip.WIPEvent = 2 or wip.WIPEvent = 1";
            }
            //已完成檢驗
            else
            {
                mod = "qcp.QCPointValue is not null";
                //mod = "wip.WIPEvent = 3";
            }


            var result = new List<QCWorkTask>();
            var sqlStr = $@"select a.OrderID,a.OPID,a.OPLTXA1,a.MAKTX,a.StartTime,a.EndTime,a.AssignDate, d.remark,d.GroupName, u.[user_name], wip.WIPEvent, wip.OrderQTY, wip.QtyTol, wip.QtyBad , wip.QtyGood , k.user_name as QCman,
                            Progress = cast( (cast(wip.QtyGood as float) + cast(wip.QtyBad as float)) / cast(a.OrderQTY as float) * 100 as int)
                            ,pt.Name,qcr.QCPointName,qcp.QCPointValue
                            from {_ConnectStr.APSDB}.dbo.Assignment as a
                            left join {_ConnectStr.APSDB}.dbo.WIP as wip ON a.OrderID = wip.OrderID and a.OPID = wip.OPID
                            left join {_ConnectStr.APSDB}.dbo.QCAssignment as q on a.OrderID = q.WorkOrderID and a.OPID = q.OPID
                            LEFT JOIN {_ConnectStr.MRPDB}.dbo.QCrule as qcr on a.OPID=qcr.id
                            LEFT JOIN {_ConnectStr.APSDB}.dbo.QCPointValue as qcp ON a.OrderID=qcp.WorkOrderID AND qcp.OPID=qcr.OPID and qcp.QCPoint=qcr.QCPoint
                            left join {_ConnectStr.AccuntDB}.dbo.[User] as u on a.Operator = u.[user_id]
                            left join {_ConnectStr.APSDB}.dbo.Device as d on a.WorkGroup=d.remark
                            left join {_ConnectStr.AccuntDB}.[dbo].[User] as k on q.QCman=k.[user_id]
                            left join {_ConnectStr.MRPDB}.[dbo].[Part] as pt on a.MAKTX=pt.Number
                            where wip.WIPEvent in (1,2,3) and qcr.QCPointName is not null and {mod} ORDER BY a.AssignDate ASC";


            #region Ver.1 語法
            //var sqlStr = $@"
            //                select a.*, d.remark,d.GroupName, u.[user_name], wip.WIPEvent, wip.OrderQTY, wip.QtyTol, wip.QtyBad , wip.QtyGood ,q.IsQC, k.user_name as QCman,
            //                Progress = cast( (cast(wip.QtyGood as float) + cast(wip.QtyBad as float)) / cast(a.OrderQTY as float) * 100 as int), RemainingCount = (a.OrderQTY - wip.QtyTol)
            //                ,pt.*
            //                from Assignment as a
            //                left join WIP as wip ON a.OrderID = wip.OrderID and a.OPID = wip.OPID
            //                left join QCAssignment as q on a.OrderID = q.WorkOrderID and a.OPID = q.OPID
            //                left join {_ConnectStr.AccuntDB}.dbo.[User] as u on a.Operator = u.[user_id]
            //                left join Device as d on a.WorkGroup=d.remark
            //                left join {_ConnectStr.AccuntDB}.[dbo].[User] as k on q.QCman=k.[user_id]
            //                left join {_ConnectStr.MRPDB}.[dbo].[Part] as pt on a.MAKTX=pt.Number
            //                where {mod} ORDER BY a.AssignDate ASC
            //                ";
            #endregion
            using (var conn = new SqlConnection(_ConnectStr.Local))
            {
                using (var comm = new SqlCommand(sqlStr, conn))
                {
                    if (conn.State != ConnectionState.Open)
                        conn.Open();
                    using (SqlDataReader SqlData = comm.ExecuteReader())
                    {
                        if (SqlData.HasRows)
                        {
                            while (SqlData.Read()) //還能讀取就持續讀取
                            {
                                result.Add(new QCWorkTask
                                {
                                    OrderId = checkNoword(SqlData["OrderId"].ToString().Trim()),
                                    OPId = checkNoword(SqlData["OPID"].ToString().Trim()),
                                    OPName = checkNoword(SqlData["OPLTXA1"].ToString().Trim()),
                                    ProductId = checkNoword(SqlData["MAKTX"].ToString().Trim()),
                                    ProductName = checkNoword(SqlData["Name"].ToString().Trim()),
                                    StartTime = string.IsNullOrEmpty(SqlData["StartTime"].ToString()) ? "N/A" : Convert.ToDateTime(SqlData["StartTime"]).ToString(_timeFormat),//checkNoword(SqlData["StartTime"].ToString().Trim()),
                                    EndTime = string.IsNullOrEmpty(SqlData["EndTime"].ToString()) ? "N/A" : Convert.ToDateTime(SqlData["EndTime"]).ToString(_timeFormat),//checkNoword(SqlData["EndTime"].ToString().Trim()),
                                    OPStatus = checkNoword(SqlData["WIPEvent"].ToString().Trim()),
                                    AssignDate = string.IsNullOrEmpty(SqlData["AssignDate"].ToString()) ? "N/A" : Convert.ToDateTime(SqlData["AssignDate"]).ToString(_dateFormat),//checkNoword(SqlData["AssignDate"].ToString().Trim()),
                                    DeleyDays = deleyday(SqlData["AssignDate"].ToString().Trim(), SqlData["EndTime"].ToString()),
                                    Deviec = checkNoword(SqlData["remark"].ToString().Trim()),
                                    DeviceGroup = checkNoword(SqlData["GroupName"].ToString().Trim()),
                                    OperatorName = checkNoword(SqlData["user_name"].ToString().Trim()),
                                    Progress = ChangeProgressIntFormat(SqlData["Progress"].ToString().Trim()),
                                    RequireNum = checkNoword(SqlData["OrderQTY"].ToString().Trim()),
                                    CompleteNum = checkNoword(SqlData["QtyGood"].ToString().Trim()),
                                    DefectiveNum = checkNoword(SqlData["QtyBad"].ToString().Trim()),
                                    //IsQC = "False",
                                    IsQC = QCStatus(IsQCDone(SqlData["OrderId"].ToString().Trim(), SqlData["OPID"].ToString().Trim()).ToString()),
                                    QCman = checkNoword(SqlData["QCman"].ToString().Trim())
                                });
                            }
                        }
                    }
                }
            }


            return result;
        }

        private string QCStatus(string isqc)
        {
            if (isqc == "" || isqc == "N/A")
            {
                return "N/A";//無須檢驗
            }
            else
            {
                return isqc;//False需要檢驗、Ture檢驗完成
            }
        }

        private object IsQCDone(string OrderID, string OPId)
        {
            string ans = "N/A";
            var result = new List<QC>();

            result = QCList(OrderID, OPId);

            if (result.Count == 0)
            {
                ans = "N/A";
            }
            else
            {
                if (result.Exists(x => x.QCPointName == "N/A"))
                {
                    ans = "N/A";
                }
                else
                {
                    if (result.Exists(x => x.QCPointValue == "N/A"))
                    {
                        ans = "False";
                    }
                    else
                    {
                        ans = "True";
                    }
                }
            }

            return ans;
        }

        private List<QC> QCList(string OrderID, string OPId)
        {
            var result = new List<QC>();
            var SqlStr = @$"SELECT a.OrderID ,a.OPID,a.MAKTX,c.WIPEvent,b.QCPoint,b.QCPointName,b.QCLSL,b.QCUSL,d.QCPointValue,d.QCToolId,d.QCunit,d.Createtime,d.Lastupdatetime
                            FROM {_ConnectStr.APSDB}.dbo.Assignment as a
                            LEFT JOIN {_ConnectStr.MRPDB}.dbo.QCrule as b on a.MAKTX=b.MAKTX AND a.OPID=b.opid 
                            LEFT JOIN {_ConnectStr.APSDB}.dbo.QCPointValue as d ON b.MAKTX=d.MAKTX AND a.OrderID=d.WorkOrderID AND b.opid=d.OPID and b.QCPoint=d.QCPoint
                            LEFT JOIN {_ConnectStr.APSDB}.dbo.WIP as c on a.OrderID=c.OrderID AND a.OPID=c.OPID
                            WHERE a.OrderID= @OrderID AND a.OPID= @OPId";

            using (var conn = new SqlConnection(_ConnectStr.Local))
            {
                using (var comm = new SqlCommand(SqlStr, conn))
                {
                    if (conn.State != ConnectionState.Open)
                        conn.Open();
                    comm.Parameters.Add(("@OrderID"), SqlDbType.NVarChar).Value = OrderID;
                    comm.Parameters.Add(("@OPId"), SqlDbType.Float).Value = OPId;
                    using (SqlDataReader SqlData = comm.ExecuteReader())
                    {
                        if (SqlData.HasRows)
                        {
                            while (SqlData.Read())
                            {
                                bool enable = true;
                                if (SqlData["WIPEvent"].ToString().Trim() == "0" || SqlData["WIPEvent"].ToString().Trim() == "3") enable = false;
                                result.Add(new QC
                                {
                                    QCPointName = checkNoword(SqlData["QCPointName"].ToString().Trim()),
                                    QCPointValue = checkNoword(SqlData["QCPointValue"].ToString().Trim()),
                                    QCResult = checkQCResult(SqlData),
                                    QCUnit = checkNoword(SqlData["QCunit"].ToString().Trim()),
                                    QCToolID = checkNoword(SqlData["QCToolId"].ToString().Trim()),
                                    CreateTime = checkNoword(SqlData["CreateTime"].ToString().Trim()),
                                    LastUpdateTime = checkNoword(SqlData["LastUpdateTime"].ToString().Trim()),
                                    Enable = enable
                                });

                            }
                        }
                    }
                }
            }
            return result;
        }

        private string deleyday(string DeadLine_t, string WillDone_t)
        {
            string result = "";
            DateTime dtDate;
            if (DateTime.TryParse(DeadLine_t, out dtDate))
            {
                DateTime start = Convert.ToDateTime(DeadLine_t);

                DateTime end = Convert.ToDateTime(WillDone_t);

                TimeSpan ts = end.Subtract(start); //兩時間天數相減

                if (ts.Days < 0)
                {
                    result = "0 天";
                }
                else
                {
                    result = ts.Days.ToString() + " 天"; //相距天數
                }
            }
            else
            {
                result = "N/A";
            }
            return result;
        }

        /// <summary>
        /// 取得檢驗工作任務清單詳細資訊
        /// </summary>
        /// <param name="OrderID">EX: 1219111091</param>
        /// <param name="OPId">EX: 40</param>
        /// <returns></returns>
        [HttpPost("QCWorkTaskDetail")]
        public List<OPDetail> QCWorkTaskDetail(string OrderID, string OPId)
        {
            var result = new List<OPDetail>();
            string baseUrl = "http://" + Request.Host.Value + "/CADFiles/";

            if (OrderID != "" && OPId != "" && OrderID != null && OPId != null)
            {
                var SqlStr = @$"SELECT a.*, d.remark,d.GroupName, u.[user_name], wip.WIPEvent, wip.QtyTol, wip.QtyGood, wip.QtyBad, q.IsQC, k.user_name as QCman,
                    Progress = cast( (cast(wip.QtyGood as float) + cast(wip.QtyBad as float)) / cast(a.OrderQTY as float) * 100 as int),RemainingCount = (a.OrderQTY - wip.QtyTol) 
                    ,pt.*
                    FROM {_ConnectStr.APSDB}.[dbo].[Assignment] as a
                    LEFT JOIN {_ConnectStr.APSDB}.[dbo].[Device] as d ON a.[WorkGroup] = d.remark
                    LEFT JOIN {_ConnectStr.APSDB}.[dbo].[WIP] as wip ON (wip.OrderID=a.OrderID and wip.OPID = a.OPID)
                    LEFT JOIN {_ConnectStr.APSDB}.[dbo].WipRegisterLog as wipr ON a.OrderID=wipr.WorkOrderID AND a.OPID=wipr.OPID
                    LEFT JOIN {_ConnectStr.AccuntDB}.[dbo].[User] as u ON a.Operator=u.[user_id]
                    LEFT JOIN {_ConnectStr.APSDB}.[dbo].[QCAssignment] as q ON (q.WorkOrderID = a.OrderID and q.OPID = a.OPID)
                    left join {_ConnectStr.AccuntDB}.[dbo].[User] as k on q.QCman=k.[user_id]
                    left join {_ConnectStr.MRPDB}.[dbo].[Part] as pt on a.MAKTX=pt.Number
                    WHERE a.OrderID =@OrderId and a.OPID = @OpId";

                using (var conn = new SqlConnection(_ConnectStr.Local))
                {
                    using (var comm = new SqlCommand(SqlStr, conn))
                    {
                        if (conn.State != ConnectionState.Open)
                            conn.Open();
                        comm.Parameters.Add(("@OrderID"), SqlDbType.NVarChar).Value = OrderID;
                        comm.Parameters.Add(("@OPId"), SqlDbType.NVarChar).Value = OPId;
                        using (SqlDataReader SqlData = comm.ExecuteReader())
                        {
                            if (SqlData.HasRows)
                            {
                                string imgPath = "N/A";
                                while (SqlData.Read())
                                {                                    
                                    if (_ConnectStr.Debug == 1)
                                        imgPath = "https://i.imgur.com/pOxVHtB.jpg";
                                    else
                                        imgPath = baseUrl + (string.IsNullOrEmpty(SqlData["ImgPath"].ToString().Trim()) ? "N/A" : SqlData["ImgPath"].ToString());


                                    result.Add(new OPDetail
                                    {
                                        OrderId = checkNoword(SqlData["OrderId"].ToString().Trim()),
                                        OPId = checkNoword(SqlData["OPID"].ToString().Trim()),
                                        OPName = checkNoword(SqlData["OPLTXA1"].ToString().Trim()),
                                        OPStatus = checkNoword(SqlData["WIPEvent"].ToString().Trim()),
                                        ProductId = checkNoword(SqlData["MAKTX"].ToString().Trim()),
                                        ProductName = checkNoword(SqlData["Name"].ToString().Trim()),
                                        StartTime = string.IsNullOrEmpty(SqlData["StartTime"].ToString()) ? "N/A" : Convert.ToDateTime(SqlData["StartTime"]).ToString(_timeFormat),//checkNoword(SqlData["StartTime"].ToString().Trim()),
                                        EndTime = string.IsNullOrEmpty(SqlData["EndTime"].ToString()) ? "N/A" : Convert.ToDateTime(SqlData["EndTime"]).ToString(_timeFormat),//checkNoword(SqlData["EndTime"].ToString().Trim()),
                                        AssignDate = string.IsNullOrEmpty(SqlData["AssignDate"].ToString()) ? "N/A" : Convert.ToDateTime(SqlData["AssignDate"]).ToString(_dateFormat),//checkNoword(SqlData["AssignDate"].ToString().Trim()),
                                        DeleyDays = deleyday(SqlData["AssignDate"].ToString().Trim(), SqlData["EndTime"].ToString()),
                                        Deviec = checkNoword(SqlData["remark"].ToString().Trim()),
                                        DeviceGroup = checkNoword(SqlData["GroupName"].ToString().Trim()),
                                        OperatorName = checkNoword(SqlData["user_name"].ToString().Trim()),
                                        Progress = ChangeProgressIntFormat(SqlData["Progress"].ToString().Trim()),
                                        RequireNum = checkNoword(SqlData["OrderQTY"].ToString().Trim()),
                                        CompleteNum = checkNoword(SqlData["QtyGood"].ToString().Trim()),
                                        DefectiveNum = checkNoword(SqlData["QtyBad"].ToString().Trim()),
                                        IsQC = "False",
                                        //IsQC = QCStatus(IsQCDone(SqlData["OrderId"].ToString().Trim(), SqlData["OPID"].ToString().Trim()).ToString()),
                                        QCman = checkNoword(SqlData["QCman"].ToString().Trim()),
                                        QtyBad = checkNoword(SqlData["QtyBad"].ToString().Trim()),
                                        ImgPath = imgPath
                                    });

                                    //判斷是否有檢驗點要顯示
                                    var QC = QCList(OrderID, OPId);
                                    if (QC.Count == 1)
                                    {
                                        if (!QC.Exists(x => x.QCPointName == "N/A"))
                                        {
                                            result[0].QCList = QC;
                                        }
                                    }
                                    else
                                    {
                                        result[0].QCList = QC;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return result;

        }

        /// <summary>
        /// 判斷檢驗結果
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private string checkQCResult([FromBody] SqlDataReader data)
        {
            string result = "N/A";
            if (data["QCPointValue"].ToString() != "" && data["QCUSL"].ToString() != "" && data["QCLSL"].ToString() != "")
            {
                double value = Convert.ToDouble(data["QCPointValue"].ToString());
                double USL = Convert.ToDouble(data["QCUSL"].ToString());
                double LSL = Convert.ToDouble(data["QCLSL"].ToString());
                if (value > USL || value < LSL)
                {
                    result = "NOGO";
                }
                else
                {
                    result = "GO";
                }
            }
            return result;
        }

        /// <summary>
        /// 填寫QCPoint
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("UpdateQCPoint")]
        public string UpdateQCPoint([FromBody] WorkQCReportRequest request)
        {
            //先檢查是否已經有data，如果沒有要先insert，如果有要update value和 lastupdatetime
            //判斷是否有QC權限
            //bool Valid = ValidQCOrder();
            //if (Valid == false)
            //{
            //    return Unauthorized();
            //}

            string result = "Update Failed!";
            int EffectRow = 0;
            var SqlStr = @$"IF NOT EXISTS(SELECT * FROM [QCPointValue] WHERE WorkOrderID = @OrderID AND OPID = @OPId AND QCPoint = (SELECT TOP(1)[QCPoint] FROM {_ConnectStr.MRPDB}.[dbo].[QCrule] WHERE MAKTX=@MAKTX AND opid=@OPId AND QCPointName=@QCPointName))
                                BEGIN 
                                     INSERT INTO [QCPointValue]
                                     ([WorkOrderID], [OPID], [MAKTX], [QCPoint], [QCPointValue], [QCToolId], [QCunit], [Createtime], [Lastupdatetime]) 
                                     VALUES
                                     (@OrderID, @OPId, @MAKTX, 
                                     (SELECT TOP(1)[QCPoint] FROM {_ConnectStr.MRPDB}.[dbo].[QCrule] WHERE MAKTX=@MAKTX AND opid=@OPId AND QCPointName=@QCPointName), 
                                     (SELECT TOP (1) [measurement_data] FROM {_ConnectStr.MeasureDB}.[dbo].[MeasureLog] WHERE U_WAVE_T_ID= @QCToolId ORDER BY _time DESC),
                                     @QCToolId, 
                                     (SELECT TOP(1)[unit] FROM {_ConnectStr.MRPDB}.[dbo].[QCrule] WHERE MAKTX=@MAKTX AND opid=@OPId AND QCPointName=@QCPointName)
                                     ,GETDATE() ,GETDATE()) 
                                END 
                           ELSE
                                BEGIN
                                     UPDATE [QCPointValue]
                                     SET QCPointValue = (SELECT TOP (1) [measurement_data] FROM {_ConnectStr.MeasureDB}.[dbo].[MeasureLog] WHERE U_WAVE_T_ID= @QCToolId ORDER BY _time DESC),
                                     QCunit=(SELECT TOP(1)[unit] FROM {_ConnectStr.MRPDB}.[dbo].[QCrule] WHERE MAKTX=@MAKTX AND opid=@OPId AND QCPointName=@QCPointName),
                                     Lastupdatetime = GETDATE()
                                     WHERE WorkOrderID = @OrderID AND OPID = @OPId AND MAKTX = @MAKTX 
                                     AND QCPoint = (SELECT TOP(1)[QCPoint] FROM {_ConnectStr.MRPDB}.[dbo].[QCrule] WHERE MAKTX=@MAKTX AND opid=@OPId AND QCPointName=@QCPointName)
                                END";
            using (var conn = new SqlConnection(_ConnectStr.Local))
            {
                using (var comm = new SqlCommand(SqlStr, conn))
                {
                    if (conn.State != ConnectionState.Open)
                        conn.Open();
                    comm.Parameters.Add(("@OrderID"), SqlDbType.NVarChar).Value = request.OrderID;
                    comm.Parameters.Add(("@OPId"), SqlDbType.Float).Value = request.OPId;
                    comm.Parameters.Add(("@MAKTX"), SqlDbType.NVarChar).Value = request.ProductId;
                    comm.Parameters.Add(("@QCPointName"), SqlDbType.NVarChar).Value = request.QCPointName;
                    comm.Parameters.Add(("@QCToolId"), SqlDbType.NVarChar).Value = request.QCToolID;
                    EffectRow = Convert.ToInt32(comm.ExecuteNonQuery());
                }
            }
            if (EffectRow > 0)
            {
                result = "Update Successful!";
            }
            else
            {
                result = "Update Failed!";
            }
            return result;
        }

        /// <summary>
        /// 取得最新無線量測數據
        /// </summary>
        /// <param name="MeasureDeviceID">輸入量具編號名稱 Ex: 03</param>
        /// <returns></returns>
        [HttpPost("NewQCPointValue")]
        public ActionResponse<List<Measuredata>> NewQCPointValue(string MeasureDeviceID)
        {
            var SqlStr = @$"SELECT TOP (1) *
                            FROM {_ConnectStr.MeasureDB}.[dbo].[MeasureLog]
                            WHERE U_WAVE_T_ID=@MeasureDeviceID
                            ORDER BY [_time] DESC";
            var result = new List<Measuredata>();
            //開啟連線
            using (var conn = new SqlConnection(_ConnectStr.Local))
            {
                using (var comm = new SqlCommand(SqlStr, conn))
                {
                    if (conn.State != ConnectionState.Open) conn.Open();
                    comm.Parameters.Add(("@MeasureDeviceID"), SqlDbType.NVarChar).Value = MeasureDeviceID;
                    using (SqlDataReader SqlData = comm.ExecuteReader())
                    {
                        if (SqlData.HasRows)
                        {
                            while (SqlData.Read())
                            {
                                result.Add(new Measuredata
                                {
                                    U_WAVE_T_ID = SqlData["U_WAVE_T_ID"].ToString().Replace("\r", "").Trim(),
                                    measurement_data = SqlData["measurement_data"].ToString().Replace("\r", "").Trim(),
                                    unit = IsMetric(SqlData["unit"].ToString().Replace("\r", "").Trim()),
                                    _time = SqlData["_time"].ToString().Replace("\r", "").Trim()
                                });
                            }
                        }
                    }
                }
            }
            //取得回傳資料給新的Response
            return new ActionResponse<List<Measuredata>>
            {
                Data = result
            };
        }

        private string IsMetric(string data)
        {
            string result = "mm";
            if (data != "M") result = "in";
            return result;
        }

        public class Measuredata
        {
            /// <summary>
            /// 量具發射器ID
            /// </summary>
            public string U_WAVE_T_ID { get; set; }

            /// <summary>
            /// 量測數值
            /// </summary>
            public string measurement_data { get; set; }

            /// <summary>
            /// 量測單位
            /// </summary>
            public string unit { get; set; }

            /// <summary>
            /// 量測時間
            /// </summary>
            public string _time { get; set; }
        }
    }
}