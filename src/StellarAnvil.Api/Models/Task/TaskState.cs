namespace StellarAnvil.Api.Models.Task;

public enum TaskState
{
    // Initial
    Created,
    
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

