#pragma warning disable 
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using ColorVision;
using ColorVision.Engine.MySql.ORM;

namespace ColorVision.Services.Devices.Spectrum.Dao
{
    public class SpectumResultModel : PKModel
    {
        public float? IntTime { get; set; }
        public int iAveNum { get; set; }
        public bool IsUseAutoIntTime { get; set; }
        public bool IsUseAutoDark { get; set; }
        public int? Pid { get; set; }
        public int? BatchId { get; set; }
        public string? fPL { get; set; }
        public string? fRi { get; set; }
        public float? fx { get; set; }
        public float? fy { get; set; }
        public float? fu { get; set; }
        public float? fv { get; set; }
        public float? fCCT { get; set; }
        public float? dC { get; set; }
        public float? fLd { get; set; }
        public float? fPur { get; set; }
        public float? fLp { get; set; }
        public float? fHW { get; set; }
        public float? fLav { get; set; }
        public float? fRa { get; set; }
        public float? fRR { get; set; }
        public float? fGR { get; set; }
        public float? fBR { get; set; }
        public float? fIp { get; set; }
        public float? fPh { get; set; }
        public float? fPhe { get; set; }
        public float? fPlambda { get; set; }
        public float? fSpect1 { get; set; }
        public float? fSpect2 { get; set; }
        public float? fInterval { get; set; }
        public DateTime? CreateDate { get; set; } = DateTime.Now;
    }
}
