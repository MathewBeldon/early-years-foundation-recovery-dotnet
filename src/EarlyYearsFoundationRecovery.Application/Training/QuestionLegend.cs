using EarlyYearsFoundationRecovery.Application.Interfaces;

namespace EarlyYearsFoundationRecovery.Application.Training;

public static class QuestionLegend
{
    public static string For(TrainingPageContent question)
    {
        if (!question.IsSummative)
        {
            return question.Heading;
        }

        // The questionnaire slice currently supports one-answer radio forms.
        // Leave multi-select content on its existing heading until its checkbox
        // submission contract is implemented rather than advertising a control
        // the view cannot submit.
        if (question.Answers.Count(answer => answer.Correct) != 1)
        {
            return question.Heading;
        }

        var prompt = string.IsNullOrWhiteSpace(question.Body) ? question.Heading : question.Body;
        return prompt + " (Select one answer)";
    }
}
