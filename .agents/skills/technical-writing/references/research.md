# Research and evidence map

This file records why the skill uses its main controls. It is maintenance
evidence, not runtime authority for a current fact, owner, commitment, or
publication decision.

## Evidence classes

- **Empirical** - a controlled experiment, corpus comparison, or systematic
  evaluation observed the behavior under stated conditions.
- **Operational** - a provider or engineering organization documents the rule
  as intended behavior or practical guidance.
- **Observed heuristic** - a recurring editorial failure with no claim of
  measured prevalence across models, prompts, or genres.

The skill combines all three. An operational guide establishes a useful default,
not universal model behavior. An empirical result keeps the model, prompt, task,
language, date, and population limits of its study.

## Rule map

| Skill control | Evidence | Limit |
| --- | --- | --- |
| Put the answer, state, or decision first | Microsoft scannable-content guidance, Google paragraph guidance, and Nielsen Norman Group inverted-pyramid practice | Practitioner guidance for web and technical content, not a universal order for tutorials or exploratory explanation |
| Keep paragraphs focused and structure semantic | Google and Microsoft documentation guidance | House-style guidance; local conventions may be more specific |
| Address code and explain why in review feedback | Google Engineering Practices | Mature organization practice, not a controlled phrase-level experiment |
| Extract decisive evidence from long context | Liu et al., *Lost in the Middle* | Results cover tested multi-document QA and retrieval tasks and 2023 models |
| Check user framing instead of agreeing by default | Sharma et al., *Towards Understanding Sycophancy in Language Models* | Five assistants and the tested tasks; does not measure every current model or interaction |
| Do not use a same-model style score as the sole gate | Zheng et al., *Judging LLM-as-a-Judge* | LLM judges also showed useful agreement; the relevant result is the documented position, verbosity, self-enhancement, and reasoning limits |
| Treat dense or formulaic model prose as a contextual risk, not a detector | Reinhart et al., *Do LLMs write like humans?* | Parallel corpora for GPT-4o and Llama 3 variants; features and rates are model- and prompt-specific |
| Preserve cultural and individual variation | Agarwal, Naaman, and Vashistha, *AI Suggestions Homogenize Writing Toward Western Styles and Diminish Cultural Nuances* | Controlled study of 118 Indian and US participants on culturally grounded tasks |
| Use explicit output and stop contracts | Anthropic prompting guidance and OpenAI Model Spec | Provider guidance describes intended or model-specific behavior, not independent prevalence evidence |
| Keep publication inside an explicit scope of authority | OpenAI Model Spec agentic-scope and side-effect guidance | Provider behavior specification; repository policy remains authoritative where stricter |
| Keep remote Markdown logical blocks on single physical lines | Observed failure at an exact-body publishing boundary | Applies to prose paragraphs and individual list items in remote fields, not repository documents or syntax-required line breaks |

Answer burial, repeated summaries, decorative headings, canned praise,
manufactured closure, and plausible unsupported specificity are observed
heuristics unless a row above supplies narrower evidence. Use them to edit an
artifact, not to identify whether a person used AI.

## Verified sources

### Comprehension and technical communication

- Microsoft Writing Style Guide,
  [Scannable content](https://learn.microsoft.com/en-us/style-guide/scannable-content/).
  Operational guidance to lead with customer-important information, use short
  clear units, support scanning, and stop after making the point.
- Google Developer Documentation Style Guide,
  [Paragraph structure](https://developers.google.com/style/paragraph-structure).
  Operational guidance to put critical information first and keep one idea per
  paragraph. The fetched page was last updated October 15, 2024.
- Google Engineering Practices,
  [How to write code review comments](https://google.github.io/eng-practices/review/reviewer/comments.html).
  Operational guidance to comment on code, explain reasoning, balance direction
  with problem statements, and label severity.
- Nielsen Norman Group,
  [Inverted Pyramid: Writing for Comprehension](https://www.nngroup.com/articles/inverted-pyramid/).
  Practitioner guidance to rank information, front-load the main point, and let
  readers stop while retaining the conclusion.

### Agent-specific risks and controls

- Liu et al.,
  [Lost in the Middle: How Language Models Use Long Contexts](https://arxiv.org/abs/2307.03172),
  TACL 2023. Empirical evaluation found position-sensitive performance on
  multi-document question answering and key-value retrieval, with relevant
  information in the middle often used less reliably.
- Sharma et al.,
  [Towards Understanding Sycophancy in Language Models](https://arxiv.org/abs/2310.13548),
  revised 2025. Empirical work found five tested assistants matched user views
  across four free-form tasks and that preference signals sometimes favored
  convincing agreement over correctness.
- Zheng et al.,
  [Judging LLM-as-a-Judge with MT-Bench and Chatbot Arena](https://arxiv.org/abs/2306.05685),
  NeurIPS 2023. Empirical evaluation documents position, verbosity,
  self-enhancement, and reasoning biases alongside useful human agreement.
- Reinhart et al.,
  [Do LLMs write like humans? Variation in grammatical and rhetorical styles](https://arxiv.org/abs/2410.16107),
  PNAS 2025. Parallel multi-genre corpora found systematic, model-specific
  grammatical and rhetorical differences and reduced human-like variation.
- Agarwal, Naaman, and Vashistha,
  [AI Suggestions Homogenize Writing Toward Western Styles and Diminish Cultural Nuances](https://arxiv.org/abs/2409.11360),
  CHI 2025. A controlled study found AI suggestions increased similarity and
  shifted Indian participants' writing toward Western styles.
- OpenAI,
  [Model Spec](https://github.com/openai/model_spec/blob/main/model_spec.md).
  Operational behavior specification covering factual accuracy, uncertainty,
  sycophancy, directness, efficient length, transformation scope, autonomy, and
  side effects. It states intended behavior, not guaranteed model performance.
- Anthropic,
  [Prompting best practices](https://platform.claude.com/docs/en/build-with-claude/prompt-engineering/claude-prompting-best-practices).
  Model-specific operational guidance for explicit output constraints, direct
  responses, long-context grounding, formatting control, overthinking,
  overeagerness, and agentic publication boundaries.

## Maintenance rules

- Recheck links and current source text before changing a rule on their basis.
- Preserve study scope when summarizing a finding. Do not project a reported
  rate onto another model, language, genre, population, or deployment.
- Prefer executable and authoritative checks for a current artifact. These
  sources justify the workflow; they do not validate the artifact's claims.
- Do not add phrase bans or an AI-text detector. Contextual overuse and reader
  cost are the defects under review.
