﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Duplicati.Library.Main.Operation
{
    internal class ListFilesHandler : IDisposable
    {
        private string m_backendurl;
        private Options m_options;
        private RestoreStatistics m_stat;

        public ListFilesHandler(string backend, Options options, RestoreStatistics stat)
        {
            m_backendurl = backend;
            m_options = options;
            m_stat = stat;
        }

        public List<string> Run()
        {
#if DEBUG
            if (!m_options.NoLocalDb)
#endif
            //Use a speedy local query
            if (System.IO.File.Exists(m_options.Dbpath))
                using (var db = new Database.LocalDatabase(m_options.Dbpath, "ListFiles"))
                {
                    var filesetId = db.GetFilesetID(m_options.RestoreTime);
                    return 
                        (from n in db.GetFiles(filesetId) 
                         select n.Path).ToList();
                }

			m_stat.LogWarning("No local database, accessing remote store", null);

            // Otherwise, grab info from remote location
            using (var tmpdb = new Library.Utility.TempFile())
            using (var db = new Database.LocalDatabase(tmpdb, "ListFiles"))
            using (var backend = new BackendManager(m_backendurl, m_options, m_stat, db))
            {
                var filter = RestoreHandler.FilterFilelist(m_options.RestoreTime);
                var fileset = filter(from n in backend.List()
                            let p = Volumes.VolumeBase.ParseFilename(n)
                            where p != null
                            select p).FirstOrDefault();

                if (fileset == null)
                    throw new Exception("No filesets found on remote target");

                List<string> res;
                using (var tmpfile = backend.Get(fileset.File.Name, fileset.File.Size, null))
                using (var rd = new Volumes.FilesetVolumeReader(RestoreHandler.GetCompressionModule(fileset.File.Name), tmpfile, m_options))
                    res = (from n in rd.Files
                            select n.Path).ToList();
                            
                backend.WaitForComplete(db, null);
                return res;
            }

        }

        public void Dispose()
        {
        }
    }
}
