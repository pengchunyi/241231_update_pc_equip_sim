using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CFX.InformationSystem;
using CFX.InformationSystem.DataTransfer;
using CFX.InformationSystem.OperatorValidation;
using CFX.InformationSystem.ProductionScheduling;
using CFX.InformationSystem.UnitValidation;
using CFX.InformationSystem.WorkOrderManagement;
using CFX.Structures;
using CFX.Structures.GenericEquipment;

namespace CFXLink
{
    public class Para
    {
        public string factory { get; set; }
        public string line { get; set; }
        public string station { get; set; }
        public string machineSN { get; set; }
        public string Remote_IP { get; set; }
        public string Remote_port { get; set; }
        public string MC_IP { get; set; }
        public string MC_Port { get; set; }
        public string MC_Name { get; set; }
        public bool UseCFX { get; set; }
        public string user { get; set; }
        public string password { get; set; }
        public string publishAddress { get; set; }
        public string SubPublishAddress { get; set; } = "/queue/Broadcast-Delta.Srcrew.A01";
        public string MyrequestUri { get; set; }
        public List<string> Topics { get; set; }
        public string ModelNumber { get; set; }//設備的銷售類型編號
        public int NumberOfLanes { get; set; } = 1;//軌道數量
        public string SN { get; set; }
        public List<StageInformation> Stages { get; set; } = new List<StageInformation>(); //設備工位描述
        public Guid Guid { get; set; }
        public Stage Stage { get; set; }//阶段用
        public int sequence { get; set; } = 1;
        public List<UnitSetPoint> unitSetPoints = new List<UnitSetPoint>();
        public List<SupportedTopic> SupportTopics { get; set; } = new List<SupportedTopic>();//設備支持的CFX能力表
        public List<Fault> Faults { get; set; } = new List<Fault>();
        public string UniqueIdentifier { get; set; } //設條條碼
        public string Vendor { get; set; } = "Delta";//供應商"原本 AMBU"
        public List<List<UnitPosition>> ListUnit_UnTest = new List<List<UnitPosition>>();
        public Recipe CurrentRecipe = new Recipe();
        //ValidateUnitsRequestURl=amqp://certification.connectedfactoryexchange.com:5672/  QPL用
        public string ValidateUnitsRequestURl { get; set; } = "amqp://certification.connectedfactoryexchange.com:5672/";
        /// <summary>
        /// CFX.Certification.Requests  QPL用
        /// </summary>
        public string ValidateUnitsHandle { get; set; } = "CFX.Certification.Requests";
        //空開測試節能
        public float    fTemperature { get; set; }
        public string COM { get; set; }
        public string StationNumber { get; set; }
        public bool bEnergyConsumption { get; set; }
        public Guid TransactionID;
    }

}

