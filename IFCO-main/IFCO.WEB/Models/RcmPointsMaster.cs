using System.ComponentModel.DataAnnotations;

namespace IFCO.WEB.Models
{
    public class RcmPointsMaster
    {
        public int SqNo { get; set; }
        public int RcmSqNo { get; set; }
        public string? VerticalId { get; set; }

        [Required(ErrorMessage = "Process is required.")]
        public string? Process { get; set; }
        public string? SubProcess { get; set; }

        [Required(ErrorMessage = "Risk Description is required.")]
        public string? RiskDescription { get; set; }
        public string? ImpactOnOccurrence { get; set; }
        public string? ScoreOfImpactOnOccurrence { get; set; }
        public string? ProbabilityOfOccurrence { get; set; }
        public string? ScoreOfProbabilityOfOccurrence { get; set; }
        public string? RiskRatingHJ { get; set; }
        public string? RiskCategory { get; set; }
        public string? SadScheduleNo { get; set; }
        public string? SadName { get; set; }
        public string? ExistenceOccurrence { get; set; }
        public string? CompletenessCutOff { get; set; }
        public string? RightsObligations { get; set; }
        public string? ValuationAllocation { get; set; }
        public string? AccuracyClassification { get; set; }
        public string? PresentationDisclosure { get; set; }

        [Required(ErrorMessage = "Control Activity is required.")]
        public string? ControlActivity { get; set; }

        [Required(ErrorMessage = "Control Department is required.")]
        public string? ControlDept { get; set; }
        public string? Designation { get; set; }
        public string? CommonCentralisedControl { get; set; }
        public string? ControlType { get; set; }
        public string? ControlTypeScore { get; set; }
        public string? LevelOfAutomation { get; set; }
        public string? ControlAutomationScore { get; set; }
        public string? ControlStrengthScore { get; set; }
        public string? ControlStrength { get; set; }
        public string? ScoreForKeyNonKey { get; set; }
        public string? KeyControlNonKeyControl { get; set; }
        public string? FrequencyOfControl { get; set; }
        public string? DesignGapYesNo { get; set; }
        public string? RecommendationRemediationPlan { get; set; }
        public string? ManagementResponse { get; set; }
        public string? TestingProcedure { get; set; }
        public string? NoOfSamplesTested { get; set; }
        public string? NatureOfTestsCarriedOut { get; set; }
        public string? OperatingEffectiveness { get; set; }
        public string? ControlActivityEvidence { get; set; }
        public string? CommentIfAny { get; set; }
        public string? ManagementReply { get; set; }
        public string? RejectionReason { get; set; }
        public string? AdminReply { get; set; }
        public string? Status { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }

        // Joined fields for display
        public string? RcmName { get; set; }
        public string? VerticalName { get; set; }
    }
}