using Isdlc.Api.Common;
using Isdlc.Application.QuestionBank;
using Isdlc.Application.QuestionBank.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Isdlc.Api.Controllers;

/// <summary>The company's forms and their questions: /api/v1/question-sets. Contract: docs/question-bank-api.md</summary>
public class QuestionSetsController(IQuestionBankService questionBankService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<QuestionSetDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<QuestionSetDto>> List(CancellationToken cancellationToken)
    {
        return await questionBankService.ListSetsAsync(cancellationToken);
    }

    /// <summary>?workType=new-software-project&amp;subcategory=External (work type may be left out).</summary>
    [HttpGet("{setCode}/questions")]
    [ProducesResponseType<IReadOnlyList<QuestionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IReadOnlyList<QuestionDto>> Questions(
        string setCode, [FromQuery] QuestionFilter filter, CancellationToken cancellationToken)
    {
        return await questionBankService.ListQuestionsAsync(setCode, filter, cancellationToken);
    }
}
