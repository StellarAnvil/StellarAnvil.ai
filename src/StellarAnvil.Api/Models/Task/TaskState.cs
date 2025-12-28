namespace StellarAnvil.Api.Models.Task;

public enum TaskState
{
    // Initial
    Created,
    
    // Manager-controlled workflow states (simplified)
    Working,        // Manager is orchestrating agents
    AwaitingUser,   // Waiting for user approval/feedback
    
    // Legacy phase-specific states (kept for backwards compatibility)
    // BA Phase
    BA_Working,
    SrBA_Reviewing,
    BA_Deliberating,
    AwaitingUser_BA,
    
    // Dev Phase
    Dev_Working,
    SrDev_Reviewing,
    Dev_Deliberating,
    AwaitingUser_Dev,
    
    // QA Phase
    QA_Working,
    SrQA_Reviewing,
    QA_Deliberating,
    AwaitingUser_QA,
    
    // Final
    Completed
}

