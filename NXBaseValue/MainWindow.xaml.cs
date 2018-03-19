using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Data.OleDb;
using System.Data;
using System.Threading;
using System.Windows.Threading;
using Accord.MachineLearning;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections;

namespace NXBaseValue
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

    public partial class MainWindow : Window
    {
        OleDbConnection conn;
        OleDbCommand cmd;
        OleDbDataAdapter adp = new OleDbDataAdapter();
        DataSet set = new DataSet();

        DateTime BeginTime;///开始时间
        DispatcherTimer timer = new DispatcherTimer();///时间调度器

        bool isComputing = false;
        int TURID = 129412;///机组id
        int flag = 1; //写入标记，0-初始 1-自动更新 2-手工调整
        string detailstr;///存储展示信息的临时字符串
        int count_display = 200;///展示字符数目
        string display_str;//展示计算时间
        ArrayList doclist = new ArrayList();//文档解析后的动态数组
        bool isFindFile = false;///是否已经打开了配置文件
        public MainWindow()
        {
            // int check = CheckParameter(95, 80, 110, 90);
            InitializeComponent();
            /////解析txt文档，读取里面的所有字段到动态数组之中
            //doclist = ParseTxtDoc("11.txt");

            timer.Interval = TimeSpan.FromMilliseconds(100);///100毫秒观察一次
            timer.Tick += OnTimerObserver;
            #region 数据库连接
            try
            {
                conn = new OleDbConnection("Provider=OraOLEDB.Oracle;User ID=imsoft;Data Source=orcl10g_my;Password=imsoft");
                conn.Open();
                if (conn.State == ConnectionState.Open)
                {
                    // MessageBox.Show("成功打开数据库");
                    cmd = new OleDbCommand();
                    cmd.CommandType = CommandType.Text;
                    cmd.Connection = conn;
                    // DataTable dddt = new DataTable();
                    // dddt.Columns.Add("列1",typeof(double));
                    // dddt.Columns.Add("列2", typeof(double));
                    // DataRow row = dddt.NewRow();
                    // row["列1"] = 11;
                    // row["列2"] = 22;
                    // dddt.Rows.Add(row);

                    // var h = from re in dddt.AsEnumerable()
                    //         select re.Field<double>("列1");
                    // double d=h.First();
                    //int j= h.Count();
                    // foreach(double hh in h)
                    // {

                    // }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            #endregion


        }

        private void OnTimerObserver(object sender, EventArgs e)////对计算线程进行时间监督
        {

            // WaitingText.Dispatcher.BeginInvoke(actionDelegate, "正在计算" + display_str);
            if (isComputing)
            {
                TimeSpan timeSp = DateTime.Now - BeginTime;
                display_str = timeSp.Hours.ToString() + ":" + timeSp.Minutes.ToString() + ":" + timeSp.Seconds.ToString();
                WaitingText.Text = "正在计算" + display_str;
            }
            else
            {
                waitingbox.Visibility = Visibility.Collapsed;
                WaitingText.Text = "计算完成，用时" + display_str;
            }
        }

        public void ComputeDataBase()
        {
            ////从参数表中取出参数详细信息
            DataTable measure_dt = QueryData("select id,name,saveindex,evaflag,isbasevalue from tb_nx_measure where evaflag is not null and isbasevalue is not null");
            /////从工况配置表中取出工况详细信息
            DataTable workmode_dt = QueryData("select id,turid,powerupper,powerlower,tempupper,templower from tb_nx_workmode where turid=" + TURID.ToString());
            int temp_index_A = 0;///引风机A入口气温存储索引
            var re1 = from h in measure_dt.AsEnumerable()
                      where h.Field<string>("name") == "送风机A入口温度"
                      select Convert.ToInt32(h.Field<object>("saveindex"));
            foreach (int d in re1)
            {
                temp_index_A = d;
            }

            int temp_index_B = 0;///引风机B入口气温存储索引
            var re2 = from h in measure_dt.AsEnumerable()
                      where h.Field<string>("name") == "送风机B入口温度"
                      select Convert.ToInt32(h.Field<object>("saveindex"));
            foreach (int d in re2)
            {
                temp_index_B = d;
            }

            int fuhe_index = 0;///功率存储索引
            var re3 = from h in measure_dt.AsEnumerable()
                      where h.Field<string>("name") == "#5机组发电机功率"
                      select Convert.ToInt32(h.Field<object>("saveindex"));
            foreach (int d in re3)
            {
                fuhe_index = d;
            }

            Action<string> waiting_action = (x) => { WaitingText.Text = x.ToString(); };///跨线程操作UI
            Func<string> num_func = () => { return num_julei.Text; };
            Action<string> detail_action = (x) => { detail.Text = x.ToString(); };
            Action scroll_action = () => { Scroll.ScrollToBottom(); };
            Func<string> zhichidu_func = () => zhichidu.Text;///应该进行规则验证
            int num = int.Parse(num_julei.Dispatcher.Invoke(num_func));///获取聚类数目
            double re_zhichidu = double.Parse(zhichidu.Dispatcher.Invoke(zhichidu_func));
            /////建立存储聚类中心和支持度的临时数据表
            DataTable tempdt = new DataTable("tempdt");
            tempdt.Columns.Add("聚类中心", typeof(double));
            tempdt.Columns.Add("支持度", typeof(double));
            ///////////////////////////////////////
            FileStream erro_log = new FileStream("erro.log", FileMode.Create);///存入硬盘错误日志
            StreamWriter error_writer = new StreamWriter(erro_log);
            error_writer.WriteLine("创建时间：" + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"));///写入日志创建时间
            error_writer.Flush();
            FileStream run_log = new FileStream("run.log", FileMode.Create);//存入硬盘运行日志
            StreamWriter run_writer = new StreamWriter(run_log);
            run_writer.WriteLine("创建时间：" + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"));///写入日志创建时间
            run_writer.Flush();
            foreach (DataRow measure_row in measure_dt.Rows)////遍历参数表中的可挖掘基准值参数
            {
                string name_str = Convert.ToString(measure_row["name"]);
                int saveindex = Convert.ToInt32(measure_row["saveindex"]);
                int evaflag = Convert.ToInt32(measure_row["evaflag"]);
                int isbasevalue = Convert.ToInt32(measure_row["isbasevalue"]);
                int paramid = Convert.ToInt32(measure_row["id"]);
                if (saveindex != temp_index_A && saveindex != temp_index_B)///判断是不是温度和负荷参数
                    if (saveindex != fuhe_index)
                        if (isbasevalue == 1)///判断是不是需要进行挖掘的参数
                        {

                            //         string strSql = "select V" + saveindex.ToString() + ",V" + fuhe_index.ToString() + ",V" + temp_index_A.ToString() + ",V" + temp_index_B.ToString() +
                            //" from tb_nx_his_run where cytime between " + "'2015-01-01'" + " and " + "'2015-02-01'"
                            //+ " or cytime between " + "'2015-04-01'" + " and " + "'2015-05-01'"
                            //+ " or cytime between " + "'2015-07-01'" + " and " + "'2015-08-01'"
                            //+ " or cytime between " + "'2015-10-01'" + " and " + "'2015-11-01'";////每季度取一个月的数据
                            string strSql = "select V" + saveindex.ToString() + ",V" + fuhe_index.ToString() + ",V" + temp_index_A.ToString() + ",V" + temp_index_B.ToString() +
             " from tb_nx_his_run";////一年数据
                            DataTable data_dt;
                            try
                            {
                                data_dt = QueryData(strSql);///从数据库取出样本信息
                            }
                            catch (Exception ex)
                            {
                                error_writer.WriteLine("从数据库取出样本错误：" + ex.Message);
                                error_writer.WriteLine("SQL语句：" + strSql);
                                error_writer.WriteLine("参数名字:" + name_str);
                                error_writer.WriteLine();
                                error_writer.Flush();
                                continue;
                            }
                            foreach (DataRow workmode_row in workmode_dt.Rows)////遍历工况
                            {
                                try
                                {
                                    int workmodeid = Convert.ToInt32((workmode_row["id"]));
                                    double powerupper = Convert.ToSingle(workmode_row["powerupper"]);
                                    double powerlower = Convert.ToSingle(workmode_row["powerlower"]);
                                    double tempupper = Convert.ToSingle(workmode_row["tempupper"]);
                                    double templower = Convert.ToSingle(workmode_row["templower"]);
                                    //////////////////////////////
                                    //string strSql = "select V" + saveindex.ToString() + " from tb_nx_his_run" + " where V" + fuhe_index.ToString()
                                    //    + " between " + powerlower.ToString() + " and " + powerupper.ToString() + " and (V" + temp_index_A.ToString() + "+V" + temp_index_B.ToString() + ")/2"
                                    //    + " between " + templower.ToString() + " and " + tempupper.ToString();////根据温度和负荷条件选取数据
                                    //DataTable data_dt = QueryData(strSql);///从数据库取出样本信息
                                    //////////////////////////////////////
                                    var data_temp = from re in data_dt.AsEnumerable()
                                                    let pa = Convert.ToDouble(re.Field<object>(0))
                                                    let gl = Convert.ToDouble(re.Field<object>(1))
                                                    let wd = (Convert.ToDouble(re.Field<object>(2)) + Convert.ToDouble(re.Field<object>(3))) / 2
                                                    where gl > powerlower && gl <= powerupper
                                                    && wd > templower && wd <= tempupper
                                                    select pa;////////挑选指定工况的参数

                                    double[][] observations = new double[data_temp.Count()][];///accord.net支持的数据格式

                                    if (data_temp.Distinct().Count() < num)////应该加入规则验证
                                    {
                                        //  MessageBox.Show("样本太少！");
                                        string temp_str = "参数：" + name_str + " id:" + paramid.ToString() + " 工况id：" + workmodeid.ToString() + " 基准值：样本太少" + " 写入时间：";
                                        detailstr += temp_str;
                                        detailstr += System.Environment.NewLine;
                                        Dispatcher.Invoke(detail_action, detailstr + ". . .");
                                        Dispatcher.Invoke(scroll_action);
                                        throw new Exception(temp_str);///抛出异常，从而记录在错误日志中
                                    }
                                    else
                                    {
                                        //for (int i = 0; i < data_dt.Rows.Count; i++)////转化成accord.net需要的计算数据格式
                                        //{
                                        //    double val = Convert.ToDouble(data_dt.Rows[i].Field<object>(0));
                                        //    observations[i] = new double[] { val };
                                        //}
                                        int temp_index = 0;
                                        foreach (double jutiyangben in data_temp)
                                        {
                                            observations[temp_index] = new double[] { jutiyangben };
                                            temp_index++;
                                        }
                                        KMeans kmeans = new KMeans(num);
                                        int[] labels = kmeans.Compute(observations);///进行聚类
                                        tempdt.Rows.Clear();///清空数据表
                                        for (int tempindex = 0; tempindex < num; tempindex++)///将结果填充数据表
                                        {
                                            tempdt.Rows.Add(new object[] { kmeans.Clusters.Centroids[tempindex][0], kmeans.Clusters.Proportions[tempindex] });
                                        }
                                        double jizhun = 0;
                                        double tempzhichidu = 0;
                                        ////检查指标是越大越好还是越小越好,评价方式 1-越低越好 2-越高越好
                                        ////找不到满足的条件的基准值怎么办？？？？
                                        if (evaflag == 2)
                                        {
                                            var jieguo = (from re in tempdt.AsEnumerable()
                                                          where re.Field<double>("支持度") >= re_zhichidu
                                                          select re).OrderByDescending(x => x.Field<double>("聚类中心")).First();
                                            jizhun = jieguo.Field<double>("聚类中心");
                                            tempzhichidu = jieguo.Field<double>("支持度");
                                        }
                                        if (evaflag == 1)
                                        {
                                            var jieguo = (from re in tempdt.AsEnumerable()
                                                          where re.Field<double>("支持度") >= re_zhichidu
                                                          select re).OrderBy(x => x.Field<double>("聚类中心")).First();
                                            jizhun = jieguo.Field<double>("聚类中心");
                                            tempzhichidu = jieguo.Field<double>("支持度");
                                        }
                                        string str_time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                        string str = "insert into TB_NX_MEASURE_INTERVAL(id,TURID,PARENTID,WORKMODEID,UPPER,UPDATETIME,FLAG) values(SEQ_NX_COMMON.NEXTVAL," + TURID.ToString() +
                                            "," + paramid.ToString() + "," + workmodeid.ToString() + "," + jizhun.ToString() + ",'" + str_time + "'," + flag.ToString() + ")";
                                        cmd.CommandText = str;
                                        cmd.ExecuteNonQuery();
                                        /////输出到界面的textblock
                                        string temp_str = "参数：" + name_str + " id:" + paramid.ToString() + " 工况id：" + workmodeid.ToString() + " 基准值：" + jizhun.ToString() + " 写入时间：" + str_time;
                                        detailstr += temp_str;
                                        detailstr += System.Environment.NewLine;
                                        if (detailstr.Length - count_display > 50)
                                            detailstr = detailstr.Substring(detailstr.Length - count_display);
                                        Dispatcher.Invoke(detail_action, detailstr + ". . .");
                                        Dispatcher.Invoke(scroll_action);///进行滑条滚动

                                        run_writer.WriteLine(temp_str);///写入运行日志 

                                    }
                                }
                                catch (Exception ex)
                                {
                                    error_writer.WriteLine("计算错误：" + ex.Message);
                                    error_writer.WriteLine();
                                    error_writer.Flush();
                                    continue;
                                }
                            }
                        }
            }
            isComputing = false;///标记计算暂停
        }
        public DataTable QueryData(string strSql)////根据sql语句查询表
        {
            set.Tables.Clear();////清空tables
            cmd.CommandText = strSql;
            adp.SelectCommand = cmd;
            DataTable dt = new DataTable();
            try
            {
                adp.Fill(set);
                //  int j = 2;
                dt = set.Tables[0];
                return dt;
            }
            catch (Exception ex)
            {
                // MessageBox.Show(ex.Message);
                throw new Exception(ex.Message);//抛出异常
                // return null;
            }
        }

        private void OnComBtn(object sender, RoutedEventArgs e)
        {
            if (isComputing == false)
            {
                isComputing = true;
                BeginTime = DateTime.Now;
                WaitingPanel.Visibility = Visibility.Visible;
                timer.Start();///开启时间监督
                Thread thread1 = new Thread(ComputeDataBase);//开启计算线程
                thread1.IsBackground = true;///设置计算线程为后台线程
                thread1.Start();
            }
        }

        #region 系统评价代码
        /// 评价枚举,1-优，2-良,3-需调整
        enum Evalution
        {
            excellent = 1,///优
            fine,///良
            adjust///需调整
        };
        ///得分结构体,替代一维数组
        struct _score
        {
            public double excel_scroe;
            public double fine_score;
            public double adjust_score;
        };

        bool isBetweenInNumber(double dest, double endPoint1, double endPoint2)//判断给定值是否位于区间内
        {
            if (endPoint1 <= endPoint2)
                if (dest < endPoint1 || dest > endPoint2)
                    return false;///不在给定区间内
            if (endPoint1 > endPoint2)
                if (dest > endPoint1 || dest < endPoint2)
                    return false;///不在给定区间内
            return true;//在给定区间内
        }

        Evalution CheckParameter(double v, double baseValue, double pcsj, double pysj)///进行参数评价，baseValue为基准值
        {
            if (!isBetweenInNumber(baseValue, pcsj, pysj))
                throw new Exception("基准值不在评优值与评差值之间");//抛出异常
            if (!isBetweenInNumber(v, pcsj, pysj))
            {
                return Evalution.adjust;///需调整

            }
            if (isBetweenInNumber(v, baseValue, pcsj))
            {
                return Evalution.fine;//良
            }
            return Evalution.excellent;//优
        }

        double[] EvalParam(double v, double baseValue, double pcsj, double pysj, double score = 0.7)///对指标评价得到隶属度向量
        {
            double excel_scroe = 0;
            double fine_score = 0;
            double adjust_score = 0;

            double distance1 = 0;
            double distance2 = 0;
            if (CheckParameter(v, baseValue, pcsj, pysj) == Evalution.adjust)///若实时值落在调整区间内
            {
                adjust_score = score;

                double temp_distance1 = Math.Abs(v - pcsj);///绝对值函数
                double temp_distance2 = Math.Abs(v - pysj);
                if (temp_distance1 < temp_distance2)
                {
                    distance1 = Math.Abs(v - pcsj);
                    distance2 = Math.Abs(v - baseValue);
                    if (distance1 + distance2 == 0)
                        throw new Exception("除数为0错误");
                    fine_score = distance2 / (distance1 + distance2) * (1 - score);
                    excel_scroe = 1 - score - fine_score;
                }
                else
                {
                    distance1 = Math.Abs(v - baseValue);
                    distance2 = Math.Abs(v - pysj);
                    if (distance1 + distance2 == 0)
                        throw new Exception("除数为0错误");
                    fine_score = distance2 / (distance1 + distance2) * (1 - score);
                    excel_scroe = 1 - score - fine_score;
                }
            }
            if (CheckParameter(v, baseValue, pcsj, pysj) == Evalution.fine)///若实时值落在良区间内
            {
                fine_score = score;
                distance1 = Math.Abs(v - pcsj);
                distance2 = Math.Abs(v - baseValue);
                if (distance1 + distance2 == 0)
                    throw new Exception("除数为0错误");
                adjust_score = distance2 / (distance1 + distance2) * (1 - score);
                excel_scroe = 1 - score - adjust_score;
            }
            if (CheckParameter(v, baseValue, pcsj, pysj) == Evalution.excellent)///若实时值落在优区间内
            {
                excel_scroe = score;
                distance1 = Math.Abs(v - baseValue);
                distance2 = Math.Abs(v - pysj);
                if (distance1 + distance2 == 0)
                    throw new Exception("除数为0错误");
                fine_score = distance2 / (distance1 + distance2) * (1 - score);
                adjust_score = 1 - score - fine_score;
            }
            double[] summery_score = new double[3];
            summery_score[0] = excel_scroe;
            summery_score[1] = fine_score;
            summery_score[2] = adjust_score;
            return summery_score;
        }
        double[] EvalSystem3Score(double[] v, double[] baseValue, double[] pcsj, double[] pysj)///对3层（底层）系统进行打分
        {
            if (!((v.Count() == baseValue.Count()) && (v.Count() == pcsj.Count()) && (v.Count() == pysj.Count())))
                throw new Exception("输入参数数组数目不一致！");
            int count = v.Count();///获得v数组的个数，估计delphi里面没有
            double[] score = new double[3] { 0, 0, 0 };
            for (int index = 0; index < count; index++)
            {
                double[] Param_Score = EvalParam(v[index], baseValue[index], pcsj[index], pysj[index]);
                score[0] += Param_Score[0];
                score[1] += Param_Score[1];
                score[2] += Param_Score[2];
            }
            score[0] /= count;
            score[1] /= count;
            score[2] /= count;
            return score;
        }

        double[] EvalSystem1and2Score(_score[] Score)///对1层和2层（高层）系统进行打分
        {
            double[] score = new double[] { 0, 0, 0 };
            int count = Score.GetLength(0);
            if (count == 0)
                throw new Exception("数组不能为空！");
            //if (Score.GetLength(1) != 3)
            //    throw new Exception("数组第二维度必须为3！");
            for (int index = 0; index < count; index++)
            {
                score[0] += Score[index].excel_scroe;
                score[1] += Score[index].fine_score;
                score[2] += Score[index].adjust_score;
            }
            score[0] /= count;
            score[1] /= count;
            score[2] /= count;
            return score;
        }

        Evalution EvalSystem(double[] score)///根据打分对系统进行评价
        {
            double max = score.Max();///获得数组中的最大值，linq表达式，估计delphi里面没有
            if (max == score[0])
                return Evalution.excellent;
            else if (max == score[1])
                return Evalution.fine;
            else
                return Evalution.adjust;
        }
        void TestEval()///测试程序，在主程序中调用以检测评价正确性
        {
            ////有A、B、C三个指标，A和B属于系统1，C属于系统2，系统1和系统2属于系统3
            ////A、B、C的v、pcsj、baseValue、pysj分别为92，80，90，100|||81，80，90，100|||60，100，70，50
            ////现在对系统1，2，3进行评价打分并得出评价评语


            ///系统1
            ///定义基本参数
            double[] v1 = { 92, 81 };
            double[] baseValue1 = { 90, 90 };
            double[] pcsj1 = { 80, 80 };
            double[] pysj1 = { 100, 100 };
            ////对系统1进行打分得到评价向量
            double[] score1 = EvalSystem3Score(v1, baseValue1, pcsj1, pysj1);
            Evalution eval1 = EvalSystem(score1);///输出结果为良

            ///系统2
            ///定义基本参数
            double[] v2 = { 60 };
            double[] baseValue2 = { 70 };
            double[] pcsj2 = { 100 };
            double[] pysj2 = { 50 };
            ////对系统2进行打分得到评价向量
            double[] score2 = EvalSystem3Score(v2, baseValue2, pcsj2, pysj2);
            Evalution eval2 = EvalSystem(score2);///输出结果为优

            ///系统3
            //double[,] _score = new double[2, 3];
            //_score[0, 0] = score1[0];
            //_score[0, 1] = score1[1];
            //_score[0, 2] = score1[2];
            //_score[1, 0] = score2[0];
            //_score[1, 1] = score2[1];
            //_score[1, 2] = score2[2];
            _score[] Score = new _score[2];
            Score[0].excel_scroe = score1[0];
            Score[0].fine_score = score1[1];
            Score[0].adjust_score = score1[2];
            Score[1].excel_scroe = score2[0];
            Score[1].fine_score = score2[1];
            Score[1].adjust_score = score2[2];
            double[] score3 = EvalSystem1and2Score(Score);
            Evalution eval3 = EvalSystem(score3);///输出结果为优
        }
        #endregion
        #region 调峰评价
        Evalution EvalSMQCKYQWD(double v)///////省煤器出口气气温度
        {
            if (v >= 298 && v <= 308)
                return Evalution.fine;
            if (v > 308)
                return Evalution.excellent;
            else
                return Evalution.adjust;
        }
        Evalution EvalKYQACKWD(double v)//////空预器A出口气温
        {
            if (v < 107.5)
                return Evalution.adjust;
            if (v >= 107.5 && v <= 112)
                return Evalution.fine;
            else
                return Evalution.excellent;
        }
        Evalution EvalKYQBCKWD(double v)//////空预器B出口气温
        {
            if (v < 107.5)
                return Evalution.adjust;
            if (v >= 107.5 && v <= 112)
                return Evalution.fine;
            else
                return Evalution.excellent;
        }
        Evalution EvalSecurity(Evalution v_SMQCKYQWD, Evalution v_KYQACKWD, Evalution v_KYQBCKWD)///调峰安全性评价
        {
            if (v_SMQCKYQWD == Evalution.adjust || v_KYQACKWD == Evalution.adjust || v_KYQBCKWD == Evalution.adjust)
                return Evalution.adjust;
            if (v_SMQCKYQWD == Evalution.excellent && v_KYQACKWD == Evalution.excellent && v_KYQBCKWD == Evalution.excellent)
                return Evalution.excellent;
            if (v_SMQCKYQWD == Evalution.fine && v_KYQACKWD == Evalution.fine && v_KYQBCKWD == Evalution.fine)
                return Evalution.fine;
            if (v_SMQCKYQWD == Evalution.fine && v_KYQACKWD == Evalution.fine && v_KYQBCKWD == Evalution.excellent)
                return Evalution.fine;
            if (v_SMQCKYQWD == Evalution.fine && v_KYQACKWD == Evalution.excellent && v_KYQBCKWD == Evalution.fine)
                return Evalution.fine;
            if (v_SMQCKYQWD == Evalution.excellent && v_KYQACKWD == Evalution.fine && v_KYQBCKWD == Evalution.fine)
                return Evalution.fine;
            return Evalution.excellent;
        }
        Evalution EvalFDMHZL(double v)////发电煤耗增量
        {
            if (v < 10)
                return Evalution.excellent;
            if (v >= 10 && v <= 20)
                return Evalution.fine;
            return Evalution.adjust;
        }
        Evalution EvalCYDLZL(double v)////厂用电率增量
        {
            if (v < 0.5)
                return Evalution.excellent;
            if (v >= 0.5 && v <= 1.3)
                return Evalution.fine;
            return Evalution.adjust;
        }
        Evalution EvalEconomical(Evalution v_FDMHZL, Evalution v_CYDLZL)/////调峰经济性
        {
            if (v_FDMHZL == Evalution.adjust || v_CYDLZL == Evalution.adjust)
                return Evalution.adjust;
            if (v_FDMHZL == Evalution.fine || v_CYDLZL == Evalution.fine)
                return Evalution.fine;
            return Evalution.excellent;
        }
        Evalution EvalSO2PFND(double v)////SO2排放浓度
        {
            if (v < 35)
                return Evalution.excellent;
            if (v >= 35 && v <= 50)
                return Evalution.fine;
            return Evalution.adjust;
        }
        Evalution EvalNOXPFND(double v)////NOX排放浓度
        {
            if (v < 50)
                return Evalution.excellent;
            if (v >= 50 && v <= 100)
                return Evalution.fine;
            return Evalution.adjust;
        }
        Evalution EvalYCPFND(double v)//// 气尘排放浓度
        {
            if (v < 5)
                return Evalution.excellent;
            if (v >= 5 && v <= 20)
                return Evalution.fine;
            return Evalution.adjust;
        }
        Evalution EvalEnvironmental(Evalution v_SO2PFND, Evalution v_NOXPFND, Evalution v_YCPFND)////调峰环保评价
        {
            if (v_SO2PFND == Evalution.adjust || v_NOXPFND == Evalution.adjust || v_YCPFND == Evalution.adjust)
                return Evalution.adjust;
            if (v_SO2PFND == Evalution.excellent && v_NOXPFND == Evalution.excellent && v_YCPFND == Evalution.excellent)
                return Evalution.excellent;
            if (v_SO2PFND == Evalution.fine && v_NOXPFND == Evalution.fine && v_YCPFND == Evalution.fine)
                return Evalution.fine;
            if (v_SO2PFND == Evalution.fine && v_NOXPFND == Evalution.fine && v_YCPFND == Evalution.excellent)
                return Evalution.fine;
            if (v_SO2PFND == Evalution.fine && v_NOXPFND == Evalution.excellent && v_YCPFND == Evalution.fine)
                return Evalution.fine;
            if (v_SO2PFND == Evalution.excellent && v_NOXPFND == Evalution.fine && v_YCPFND == Evalution.fine)
                return Evalution.fine;
            return Evalution.excellent;
        }
        #endregion
        #region 节煤潜力
        double ComputeJieMei(double v, double vBase, double minDu, int PingjiaTexing)//v:事实值，vBase基准值，minDu：耗差因子,PingjiaTexing:评价特性0-越低越好，1-越高越好
        {
            ///数据清洗
            if (minDu < 0)
                throw new Exception("敏度应为非负数");
            if ((PingjiaTexing != 0) && (PingjiaTexing != 1))
                throw new Exception("评价特性值应为0或1");
            //开始计算
            double JiemeiQianli = 0;///节煤潜力
            if (PingjiaTexing == 0)
            {
                if (v < vBase)
                {
                    JiemeiQianli = 0;
                    return JiemeiQianli;
                }
                else
                {
                    JiemeiQianli = (v - vBase) * minDu;
                    return JiemeiQianli;
                }
            }
            else 
            {
                if (v > vBase)
                {
                    JiemeiQianli = 0;
                    return JiemeiQianli;
                }
                else
                {
                    JiemeiQianli = (vBase - v) * minDu;
                    return JiemeiQianli;
                }
            }
        }
        #endregion
        private ArrayList ParseTxtDoc(string filename)/////解析txt文本
        {
            ArrayList list = new ArrayList();///新建动态数组，最后会被返回
            FileStream file = new FileStream(filename, FileMode.Open);
            StreamReader read = new StreamReader(file, Encoding.Default);///新建读文件流
            string temp_str = "";///初始化临时读取到的字符串
            int count_field = 8;///默认一行应该有几个字段
            do
            {
                temp_str = read.ReadLine();///读取一行
                ArrayList array = ParseString.FindAllString(temp_str);///从这一行中取得各个字段到动态数组中
                if (array.Count == count_field)////如果读到的字段数目正确
                {
                    try
                    {
                        for (int index = 1; index < array.Count; index++)
                        {
                            double rel = Convert.ToDouble(array[index]);///从第二个字段开始判断读到的字段能否被解析为double类型数据
                        }
                        list.Add(array);////进行到这一步，说明上面的循环没有异常抛出，则将动态数组添加到返回值中
                    }
                    catch
                    {
                        continue;///捕捉到异常，则跳过这一行，对下一行进行解析
                    }
                }
            }
            while (temp_str != null);////判断是否都到了文件的最后位置
            return list;
        }

        private void OnComFromSettingBtn(object sender, RoutedEventArgs e)
        {
            if (isFindFile == false)
            {
                MessageBox.Show("未打开配置文件！");
                return;
            }
            if (isComputing == false)
            {
                isComputing = true;
                BeginTime = DateTime.Now;
                WaitingPanel.Visibility = Visibility.Visible;
                timer.Start();///开启时间监督
                Thread thread1 = new Thread(ComputeDataBaseFromSetting);//开启计算线程
                thread1.IsBackground = true;///设置计算线程为后台线程
                thread1.Start();
            }
        }
        private void ComputeDataBaseFromSetting()
        {
            ////从参数表中取出参数详细信息
            DataTable measure_dt = QueryData("select id,name,saveindex,evaflag,isbasevalue from tb_nx_measure where evaflag is not null and isbasevalue is not null");
            /////从工况配置表中取出工况详细信息
            DataTable workmode_dt = QueryData("select id,turid,powerupper,powerlower,tempupper,templower from tb_nx_workmode where turid=" + TURID.ToString());
            int temp_index_A = 0;///引风机A入口气温存储索引
            var re1 = from h in measure_dt.AsEnumerable()
                      where h.Field<string>("name") == "送风机A入口温度"
                      select Convert.ToInt32(h.Field<object>("saveindex"));
            foreach (int d in re1)
            {
                temp_index_A = d;
            }

            int temp_index_B = 0;///引风机B入口气温存储索引
            var re2 = from h in measure_dt.AsEnumerable()
                      where h.Field<string>("name") == "送风机B入口温度"
                      select Convert.ToInt32(h.Field<object>("saveindex"));
            foreach (int d in re2)
            {
                temp_index_B = d;
            }

            int fuhe_index = 0;///功率存储索引
            var re3 = from h in measure_dt.AsEnumerable()
                      where h.Field<string>("name") == "#5机组发电机功率"
                      select Convert.ToInt32(h.Field<object>("saveindex"));
            foreach (int d in re3)
            {
                fuhe_index = d;
            }
            int num = int.Parse(Dispatcher.Invoke(() => { return num_julei.Text; }));///获取聚类数目
            double re_zhichidu = double.Parse(Dispatcher.Invoke(() => { return zhichidu.Text; }));
            /////建立存储聚类中心和支持度的临时数据表
            DataTable tempdt = new DataTable("tempdt");
            tempdt.Columns.Add("聚类中心", typeof(double));
            tempdt.Columns.Add("支持度", typeof(double));
            ///////////////////////////////////////
            FileStream settinglog = new FileStream("Settingerro.log", FileMode.Create);///存入硬盘错误日志
            StreamWriter error_writer = new StreamWriter(settinglog);
            error_writer.WriteLine("创建时间：" + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"));///写入日志创建时间
            error_writer.Flush();
            
            /////遍历解析出来的动态数组
            int foreparamid = 0;//为了优化程序，在遍历doc时，记录前一行所得的参数id
            int index_foreach = 0;////foreach循环的次数
            DataTable data_dt = new DataTable();///存放一年数据的表格
            detailstr = "";///先清空展示列表中的数据
            foreach (ArrayList array in doclist)
            {
                index_foreach++;///开始递增
                string name = Convert.ToString(array[0]);////从数组中取出参数名字
                int saveindex = 0;
                int evaflag = 0;
                int isbasevalue = 0;
                int paramid = 0;
                ////从参数配置表中取出与txt文档相同参数名称的记录
                var re_test = from h in measure_dt.AsEnumerable()
                              where h.Field<string>("name") == name
                              select h;
                ////判断结果是否为空
                if (re_test == null)
                {
                    WriteToScreen("从参数配置表取结果失败！");
                    error_writer.WriteLine("从参数配置表取结果失败！");
                    error_writer.Flush();
                    continue;
                }
                /////判断是不是找到所需记录
                if (re_test.Count() == 0)
                {
                    WriteToScreen("参数{" + name + "}在数据库中未找到！");
                    error_writer.WriteLine("参数{" + name + "}在数据库中未找到！");
                    continue;
                }
                ////判断是否找到了多条记录
                if (re_test.Count() > 1)
                {
                    WriteToScreen("参数{" + name + "}在数据库中出现多次！");
                    error_writer.WriteLine("参数{" + name + "}在数据库中出现多次！");
                    continue;
                }
                /////找到一条记录后，对里面的所有字段进行解析
                foreach (var d in re_test)
                {
                    saveindex = Convert.ToInt32(d.Field<object>("saveindex"));
                    evaflag = Convert.ToInt32(d.Field<object>("evaflag"));
                    isbasevalue = Convert.ToInt32(d.Field<object>("isbasevalue"));
                    paramid = Convert.ToInt32(d.Field<object>("id"));
                }
                if (index_foreach == 1)
                    foreparamid = paramid;////把第一个参数id给foreparamid
                                          ////从动态数组中取出负荷上下限，温度上下限
                double powerlower = Convert.ToDouble(array[1]);
                double powerupper = Convert.ToDouble(array[2]);
                double templower = Convert.ToDouble(array[3]);
                double tempupper = Convert.ToDouble(array[4]);
                int workid = 0;
                var re_workmode = from h in workmode_dt.AsEnumerable()
                                  where Convert.ToDouble(h.Field<object>("powerlower")) == powerlower
                                  && Convert.ToDouble(h.Field<object>("powerupper")) == powerupper
                                  && Convert.ToDouble(h.Field<object>("templower")) == templower
                                  && Convert.ToDouble(h.Field<object>("tempupper")) == tempupper
                                  select h;
                ////判断结果是否为空
                if (re_workmode == null)
                    continue;
                /////判断是不是找到所需记录
                if (re_workmode.Count() == 0)
                {
                    WriteToScreen("参数{ " + name + "}之工况" + powerlower.ToString() + "," + powerupper.ToString() + "," + templower.ToString() + "," + tempupper.ToString() + "在数据库中未找到！");
                    error_writer.WriteLine("参数{" + name + "}之工况" + powerlower.ToString() + "," + powerupper.ToString() + "," + templower.ToString() + "," + tempupper.ToString() + "在数据库中未找到！");
                    continue;
                }
                ////判断是否找到了多条记录
                if (re_workmode.Count() > 1)
                {
                    WriteToScreen("参数{" + name + "}之工况" + powerlower.ToString() + "," + powerupper.ToString() + "," + templower.ToString() + "," + tempupper.ToString() + "在数据库中出现多次！");
                    error_writer.WriteLine("参数{" + name + "}之工况" + powerlower.ToString() + "," + powerupper.ToString() + "," + templower.ToString() + "," + tempupper.ToString() + "在数据库中出现多次！");
                    continue;
                }
                ///取出工况id
                foreach (var d in re_workmode)
                    workid = Convert.ToInt32(d.Field<object>("id"));
                ///从动态数组中读取设计值，余量，计算标识
                double designVal = Convert.ToDouble(array[5]);
                double marginVal = Convert.ToDouble(array[6]);
                double comFlag = Convert.ToDouble(array[7]);///0-能计算出基准值则用基准值，否则用设计值
                                                            ///1-全用基准值，无法计算则跳过
                                                            ///2-全用设计值
                                                            ///3-基准值和设计值之间的绝对误差超过余量，或者无法计算基准值，则用设计值
                if (!(comFlag == 0 || comFlag == 1 || comFlag == 2 || comFlag == 3))
                {
                    string temp_str = "计算标识错误";
                    WriteToScreen(temp_str);
                    error_writer.WriteLine(temp_str);
                    error_writer.Flush();
                    continue;
                }
                if (saveindex == temp_index_A)
                {
                    WriteToScreen("参数为引风机A入口气温，不需要挖掘！");
                    error_writer.WriteLine("参数为引风机A入口气温，不需要挖掘！");
                    error_writer.Flush();
                    continue;
                }
                if (saveindex == temp_index_B)
                {
                    WriteToScreen("参数为引风机B入口气温，不需要挖掘！");
                    error_writer.WriteLine("参数为引风机B入口气温，不需要挖掘！");
                    error_writer.Flush();
                    continue;
                }
                if (isbasevalue != 1)
                {
                    WriteToScreen("基准值标识不为1，不需要挖掘！");
                    error_writer.WriteLine("基准值标识不为1，不需要挖掘！");
                    error_writer.Flush();
                    continue;
                }
                if (comFlag == 2)////如果标识为2,下面的就不需要计算了
                {
                    string str_time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    ///先删除基准值表格中原先已经存在的数据
                    string deleteStr = "delete from tb_nx_measure_interval where turid=" + TURID.ToString() + " and " + "PARENTID=" + paramid.ToString() + " and " + "WORKMODEID=" + workid.ToString();
                    cmd.CommandText = deleteStr;
                    cmd.ExecuteNonQuery();
                    /////插入数据
                    string str = "insert into TB_NX_MEASURE_INTERVAL(id,TURID,PARENTID,WORKMODEID,UPPER,UPDATETIME,FLAG) values(SEQ_NX_COMMON.NEXTVAL," + TURID.ToString() +
                        "," + paramid.ToString() + "," + workid.ToString() + "," + designVal.ToString() + ",'" + str_time + "'," + flag.ToString() + ")";
                    cmd.CommandText = str;
                    cmd.ExecuteNonQuery();
                    ///写入日志
                    string temp_str = "参数：" + name + " id:" + paramid.ToString() + " 工况id：" + workid.ToString() + " 设计值：" + designVal.ToString() + " 写入时间：" + str_time;
                    error_writer.WriteLine(temp_str);
                    error_writer.Flush();
                    WriteToScreen(temp_str);
                    continue;
                }
                string strSql = "select V" + saveindex.ToString() + ",V" + fuhe_index.ToString() + ",V" + temp_index_A.ToString() + ",V" + temp_index_B.ToString() +
" from tb_nx_his_run";////一年数据
                try
                {
                    if (index_foreach != 1 && foreparamid == paramid)///如果不是第一行并且现在的参数id和之前的参数id相等
                        ;
                    else
                    {
                        data_dt.Clear();
                        data_dt = QueryData(strSql);///从数据库取出样本信息
                    }
                    foreparamid = paramid;
                }
                catch (Exception ex)
                {
                    error_writer.WriteLine("从数据库取出样本错误：" + ex.Message);
                    error_writer.WriteLine("SQL语句：" + strSql);
                    error_writer.WriteLine("参数名字:" + name);
                    error_writer.WriteLine();
                    error_writer.Flush();
                    continue;
                }
                //////////////////////////////////////
                var data_temp = from re in data_dt.AsEnumerable()
                                let pa = Convert.ToDouble(re.Field<object>(0))
                                let gl = Convert.ToDouble(re.Field<object>(1))
                                let wd = (Convert.ToDouble(re.Field<object>(2)) + Convert.ToDouble(re.Field<object>(3))) / 2
                                where gl > powerlower && gl <= powerupper
                                && wd > templower && wd <= tempupper
                                select pa;////////挑选指定工况的参数

                double[][] observations = new double[data_temp.Count()][];///accord.net支持的数据格式
                try
                {
                    if (data_temp.Distinct().Count() < num)////应该加入规则验证
                    {
                        //  MessageBox.Show("样本太少！");
                        string temp_str = "参数：" + name + " id:" + paramid.ToString() + " 工况id：" + workid.ToString() + " 基准值：样本太少" + " 写入时间：";
                        detailstr += temp_str;
                        detailstr += System.Environment.NewLine;
                        //Dispatcher.Invoke(detail_action, detailstr + ". . .");
                        //Dispatcher.Invoke(scroll_action);
                        throw new Exception(temp_str);///抛出异常，从而记录在错误日志中
                    }
                    else
                    {
                        int temp_index = 0;
                        foreach (double jutiyangben in data_temp)
                        {
                            observations[temp_index] = new double[] { jutiyangben };
                            temp_index++;
                        }
                        KMeans kmeans = new KMeans(num);
                        int[] labels = kmeans.Compute(observations);///进行聚类
                        tempdt.Rows.Clear();///清空数据表
                        for (int tempindex = 0; tempindex < num; tempindex++)///将结果填充数据表
                        {
                            tempdt.Rows.Add(new object[] { kmeans.Clusters.Centroids[tempindex][0], kmeans.Clusters.Proportions[tempindex] });
                        }
                        double jizhun = 0;
                        double tempzhichidu = 0;
                        ////检查指标是越大越好还是越小越好,评价方式 1-越低越好 2-越高越好
                        ////找不到满足的条件的基准值怎么办？？？？
                        if (evaflag == 2)
                        {
                            var jieguo = (from re in tempdt.AsEnumerable()
                                          where re.Field<double>("支持度") >= re_zhichidu
                                          select re).OrderByDescending(x => x.Field<double>("聚类中心")).First();
                            jizhun = jieguo.Field<double>("聚类中心");
                            tempzhichidu = jieguo.Field<double>("支持度");
                        }
                        if (evaflag == 1)
                        {
                            var jieguo = (from re in tempdt.AsEnumerable()
                                          where re.Field<double>("支持度") >= re_zhichidu
                                          select re).OrderBy(x => x.Field<double>("聚类中心")).First();
                            jizhun = jieguo.Field<double>("聚类中心");
                            tempzhichidu = jieguo.Field<double>("支持度");
                        }
                        if (comFlag == 3)
                        {
                            if (Math.Abs(jizhun - designVal) > marginVal)
                            {
                                ///先删除基准值表格中原先已经存在的数据
                                string deleteStr1 = "delete from tb_nx_measure_interval where turid=" + TURID.ToString() + " and " + "PARENTID=" + paramid.ToString() + " and " + "WORKMODEID=" + workid.ToString();
                                cmd.CommandText = deleteStr1;
                                cmd.ExecuteNonQuery();
                                ////插入数据库数据
                                string str_time1 = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                string str1 = "insert into TB_NX_MEASURE_INTERVAL(id,TURID,PARENTID,WORKMODEID,UPPER,UPDATETIME,FLAG) values(SEQ_NX_COMMON.NEXTVAL," + TURID.ToString() +
                                    "," + paramid.ToString() + "," + workid.ToString() + "," + jizhun.ToString() + ",'" + str_time1 + "'," + flag.ToString() + ")";
                                cmd.CommandText = str1;
                                cmd.ExecuteNonQuery();
                                ///写入日志
                                string temp_str1 = "参数：" + name + " id:" + paramid.ToString() + " 工况id：" + workid.ToString() + " 设计值：" + designVal.ToString() + " 写入时间：" + str_time1;
                                error_writer.WriteLine(temp_str1);
                                error_writer.Flush();
                                WriteToScreen(temp_str1);
                                continue;
                            }
                        }
                        ///先删除基准值表格中原先已经存在的数据
                        string deleteStr = "delete from tb_nx_measure_interval where turid=" + TURID.ToString() + " and " + "PARENTID=" + paramid.ToString() + " and " + "WORKMODEID=" + workid.ToString();
                        cmd.CommandText = deleteStr;
                        cmd.ExecuteNonQuery();
                        ////插入数据库数据
                        string str_time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        string str = "insert into TB_NX_MEASURE_INTERVAL(id,TURID,PARENTID,WORKMODEID,UPPER,UPDATETIME,FLAG) values(SEQ_NX_COMMON.NEXTVAL," + TURID.ToString() +
                            "," + paramid.ToString() + "," + workid.ToString() + "," + jizhun.ToString() + ",'" + str_time + "'," + flag.ToString() + ")";
                        cmd.CommandText = str;
                        cmd.ExecuteNonQuery();
                        /////输出到界面的textblock
                        string temp_str = "参数：" + name + " id:" + paramid.ToString() + " 工况id：" + workid.ToString() + " 基准值：" + jizhun.ToString() + " 写入时间：" + str_time;
                        WriteToScreen(temp_str);
                        error_writer.WriteLine(temp_str);///写入运行日志
                        error_writer.Flush();
                    }
                }
                catch (Exception ex)
                {
                    if (comFlag == 0 || comFlag == 3)
                    {
                        ///先删除基准值表格中原先已经存在的数据
                        string deleteStr = "delete from tb_nx_measure_interval where turid=" + TURID.ToString() + " and " + "PARENTID=" + paramid.ToString() + " and " + "WORKMODEID=" + workid.ToString();
                        cmd.CommandText = deleteStr;
                        cmd.ExecuteNonQuery();
                        /////插入数据
                        string str_time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        string str = "insert into TB_NX_MEASURE_INTERVAL(id,TURID,PARENTID,WORKMODEID,UPPER,UPDATETIME,FLAG) values(SEQ_NX_COMMON.NEXTVAL," + TURID.ToString() +
                            "," + paramid.ToString() + "," + workid.ToString() + "," + designVal.ToString() + ",'" + str_time + "'," + flag.ToString() + ")";
                        cmd.CommandText = str;
                        cmd.ExecuteNonQuery();
                        ////写入运行日志
                        error_writer.WriteLine("计算错误,以设计值" + designVal + "代替：" + ex.Message + str_time);
                        error_writer.Flush();
                        WriteToScreen("计算错误,以设计值" + designVal + "代替：" + ex.Message + str_time);
                        continue;
                    }
                    if (comFlag == 1)
                    {
                        ////写入运行日志
                        error_writer.WriteLine("计算错误,未写入数据库：" + ex.Message);
                        error_writer.Flush();
                        WriteToScreen("计算错误,未写入数据库：" + ex.Message);
                        continue;
                    }
                }
            }
            isComputing = false;///标记计算暂停
            error_writer.Close();
            settinglog.Close();


        }
        void WriteToScreen(string str)///写入到界面字符串
        {
            /////输出到界面的textblock
            detailstr += str;
            detailstr += System.Environment.NewLine;
            if (detailstr.Length - count_display > 50)
                detailstr = detailstr.Substring(detailstr.Length - count_display);
            Dispatcher.Invoke(new Action<string>((x) => { detail.Text = x.ToString(); }), detailstr + ". . .");
            Dispatcher.Invoke(() => { Scroll.ScrollToBottom(); });///进行滑条滚动
        }
        private void OnOpenFile(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Forms.OpenFileDialog openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
                openFileDialog1.InitialDirectory = "c:\\";
                openFileDialog1.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
                openFileDialog1.FilterIndex = 2;
                openFileDialog1.RestoreDirectory = true;
                if (openFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    //此处做你想做的事 ...=openFileDialog1.FileName; 
                    //string filename = "";
                    doclist = ParseTxtDoc(openFileDialog1.FileName);
                    isFindFile = true;
                    display_isFindFile.Text = "已打开配置文件";
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }
        }
    }
}

