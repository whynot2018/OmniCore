﻿using OmniCore.Model.Interfaces;
using SQLite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OmniCore.Model.Interfaces.Data;
using OmniCore.Model.Eros.Data;
using OmniCore.Model.Enums;

namespace OmniCore.Model.Eros
{
    public class ErosRepository
    {
        private static readonly ErosRepository instance = new ErosRepository();
        public static ErosRepository Instance
        {
            get
            {
                return instance;
            }
        }

        private readonly string DbPath;
        //private string DbConnectionString;

        private ErosRepository()
        {
            DbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "omnicore.db3");
            //DbConnectionString = $"Data Source={DbPath}";
            Initialize();
        }

        public void Initialize()
        {
            try
            {
                using (var conn = new SQLiteConnection(DbPath))
                {
                    conn.BeginTransaction();
                    conn.CreateTable<ErosPod>();
                    conn.CreateTable<ErosAlertStates>();
                    conn.CreateTable<ErosBasalSchedule>();
                    conn.CreateTable<ErosFault>();
                    conn.CreateTable<ErosStatus>();
                    conn.CreateTable<ErosUserSettings>();
                    conn.CreateTable<ErosMessageExchangeParameters>();
                    conn.CreateTable<ErosMessageExchangeResult>();
                    conn.CreateTable<ErosMessageExchangeStatistics>();
                    conn.CreateTable<ErosProfile>();
                    conn.CreateTable<ErosRadioPreferences>();

                    if (conn.Table<ErosProfile>().Count() == 0)
                    {
                        conn.Insert(new ErosProfile()
                        {
                            Created = DateTime.UtcNow,
                            BasalSchedule = new decimal[]
                                { 0.05m, 0.05m, 0.05m, 0.05m, 0.05m, 0.05m, 0.05m, 0.05m,
                                  0.05m, 0.05m, 0.05m, 0.05m, 0.05m, 0.05m, 0.05m, 0.05m,
                                  0.05m, 0.05m, 0.05m, 0.05m, 0.05m, 0.05m, 0.05m, 0.05m,
                                  0.05m, 0.05m, 0.05m, 0.05m, 0.05m, 0.05m, 0.05m, 0.05m,
                                  0.05m, 0.05m, 0.05m, 0.05m, 0.05m, 0.05m, 0.05m, 0.05m,
                                  0.05m, 0.05m, 0.05m, 0.05m, 0.05m, 0.05m, 0.05m, 0.05m
                                },
                            UtcOffset = 0
                        });
                    }

                    if (conn.Table<ErosRadioPreferences>().Count() == 0)
                    {
#if DEBUG
                        conn.Insert(new ErosRadioPreferences()
                        {
                            ConnectToAny = false,
                            PreferredRadios = new Guid[]
                            {
                                Guid.Parse("00000000-0000-0000-0000-886b0fec4d1a")
                            }
                        });
#else
                        conn.Insert(new ErosRadioPreferences()
                        {
                            ConnectToAny = true
                        });
#endif
                    }

                    conn.Commit();
                }
            }
            catch (SQLiteException sle)
            {
                Console.WriteLine($"Error: {sle}");
                throw sle;
            }
        }

        public SQLiteConnection GetConnection()
        {
            return new SQLiteConnection(DbPath);
        }

        public ErosPod LoadCurrent()
        {
            using (var conn = GetConnection())
            {
                return WithRelations(conn.Table<ErosPod>()
                    .Where(x => !x.Archived)
                    .OrderByDescending(x => x.Created)
                    .FirstOrDefault(), conn);
            }
        }

        public ErosPod Load(uint lot, uint tid)
        {
            using (var conn = GetConnection())
            {
                return WithRelations(conn.Table<ErosPod>()
                    .FirstOrDefault(x => x.Lot == lot && x.Serial == tid), conn);
            }
        }

        public ErosRadioPreferences GetRadioPreferences()
        {
            using (var conn = GetConnection())
            {
                return conn.Table<ErosRadioPreferences>().Single();
            }
        }

            public ErosPod GetLastActivated()
        {
            using (var conn = GetConnection())
            {
                return WithRelations(conn.Table<ErosPod>().OrderByDescending(x => x.ActivationDate)
                    .FirstOrDefault(), conn);
            }
        }

        public IProfile GetProfile()
        {
            using (var conn = new SQLiteConnection(DbPath))
            {
                return conn.Table<ErosProfile>()
                    .OrderByDescending(x => x.Id)
                    .FirstOrDefault();
            }
        }

        public void Save(IProfile profile)
        {
            using (var conn = new SQLiteConnection(DbPath))
            {
                profile.Id = conn.InsertOrReplace(profile, typeof(ErosProfile));
            }
        }

        public void Save(IPod pod, IMessageExchangeResult result = null)
        {
            using (var conn = new SQLiteConnection(DbPath))
            {
                conn.BeginTransaction();
                conn.InsertOrReplace(pod);

                if (result != null)
                {
                    if (result.Statistics != null)
                    {
                        result.Statistics.PodId = pod.Id;
                        result.Statistics.Created = DateTime.UtcNow;
                        result.Statistics.Id = conn.InsertOrReplace(result.Statistics, typeof(ErosMessageExchangeStatistics));
                        result.StatisticsId = result.Statistics.Id;
                    }

                    if (result.ExchangeParameters != null)
                    {
                        result.ExchangeParameters.PodId = pod.Id;
                        result.ExchangeParameters.Created = DateTime.UtcNow;
                        result.ExchangeParameters.Id = conn.InsertOrReplace(result.ExchangeParameters, typeof(ErosMessageExchangeParameters));
                        result.ParametersId = result.ExchangeParameters.Id;
                    }

                    if (result.Success && result.AlertStates != null)
                    {
                        result.AlertStates.PodId = pod.Id;
                        result.AlertStates.Created = DateTime.UtcNow;
                        result.AlertStates.Id = conn.InsertOrReplace(result.AlertStates, typeof(ErosAlertStates));
                        result.AlertStatesId = result.AlertStates.Id;
                        pod.LastAlertStates = result.AlertStates;
                    }

                    if (result.Success && result.BasalSchedule != null)
                    {
                        result.BasalSchedule.PodId = pod.Id;
                        result.BasalSchedule.Created = DateTime.UtcNow;
                        result.BasalSchedule.Id = conn.InsertOrReplace(result.BasalSchedule, typeof(ErosBasalSchedule));
                        result.BasalScheduleId = result.BasalSchedule.Id;
                        pod.LastBasalSchedule = result.BasalSchedule;
                    }

                    if (result.Success && result.Fault != null)
                    {
                        result.Fault.PodId = pod.Id;
                        result.Fault.Created = DateTime.UtcNow;
                        result.Fault.Id = conn.InsertOrReplace(result.Fault, typeof(ErosFault));
                        result.FaultId = result.Fault.Id;
                        pod.LastFault = result.Fault;
                    }

                    if (result.Success && result.Status != null)
                    {
                        result.Status.PodId = pod.Id;
                        result.Status.Created = DateTime.UtcNow;
                        result.Status.Id = conn.InsertOrReplace(result.Status, typeof(ErosStatus));
                        result.StatusId = result.Status.Id;
                        pod.LastStatus = result.Status;
                    }

                    if (result.Success && result.UserSettings != null)
                    {
                        result.UserSettings.PodId = pod.Id;
                        result.UserSettings.Created = DateTime.UtcNow;
                        result.UserSettings.Id = conn.InsertOrReplace(result.UserSettings, typeof(ErosUserSettings));
                        result.UserSettingsId = result.UserSettings.Id;
                        pod.LastUserSettings = result.UserSettings;
                    }

                    result.PodId = pod.Id;
                    result.Id = conn.InsertOrReplace(result, typeof(ErosMessageExchangeResult));
                }

                conn.Commit();
            }
        }

        public List<ErosMessageExchangeResult> GetHistoricalResultsForRemoteApp(long startAfterId)
        {
            using (var conn = GetConnection())
            {
                return WithHistoricalRelations(conn.Table<ErosMessageExchangeResult>()
                    .Where(x => x.Id > startAfterId && x.Success)
                    .OrderBy(x => x.Id), conn);
            }
        }

        private List<ErosMessageExchangeResult> WithHistoricalRelations(TableQuery<ErosMessageExchangeResult> tableQuery,
            SQLiteConnection conn)
        {
            var list = new List<ErosMessageExchangeResult>();
            foreach(var result in tableQuery)
            {
                if (result.StatusId.HasValue)
                    result.Status = conn.Table<ErosStatus>().Single(x => x.Id == result.StatusId.Value);

                if (result.BasalScheduleId.HasValue)
                    result.BasalSchedule = conn.Table<ErosBasalSchedule>().Single(x => x.Id == result.BasalScheduleId.Value);

                if (result.FaultId.HasValue)
                    result.Fault = conn.Table<ErosFault>().Single(x => x.Id == result.FaultId.Value);

                list.Add(result);
            }
            return list;
        }


        private ErosPod WithRelations(ErosPod pod, SQLiteConnection conn)
        {
            if (pod == null)
                return null;

            var tempBasal = conn.Table<ErosMessageExchangeResult>()
                .Where(x => x.PodId == pod.Id && x.Success && x.Type == RequestType.SetTempBasal)
                .OrderByDescending(x => x.Id)
                .FirstOrDefault();

            var tempBasalCancel = conn.Table<ErosMessageExchangeResult>()
                .Where(x => x.PodId == pod.Id && x.Success && x.Type == RequestType.CancelTempBasal)
                .OrderByDescending(x => x.Id)
                .FirstOrDefault();

            pod.LastTempBasalResult = null;
            if (tempBasal != null &&
                (tempBasalCancel == null || tempBasalCancel.Id < tempBasal.Id))
            {
                pod.LastTempBasalResult = tempBasal;
            }

            pod.LastAlertStates = conn.Table<ErosAlertStates>().Where(x => x.PodId == pod.Id)
                .OrderByDescending(x => x.Id)
                .FirstOrDefault();

            pod.LastBasalSchedule = conn.Table<ErosBasalSchedule>().Where(x => x.PodId == pod.Id)
                .OrderByDescending(x => x.Id)
                .FirstOrDefault();

            pod.LastFault = conn.Table<ErosFault>().Where(x => x.PodId == pod.Id)
                .OrderByDescending(x => x.Id)
                .FirstOrDefault();

            pod.LastStatus = conn.Table<ErosStatus>().Where(x => x.PodId == pod.Id)
                .OrderByDescending(x => x.Id)
                .FirstOrDefault();

            pod.LastUserSettings = conn.Table<ErosUserSettings>().Where(x => x.PodId == pod.Id)
                .OrderByDescending(x => x.Id)
                .FirstOrDefault();

            return pod;
        }
    }
}
