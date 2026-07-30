# GitHub review-thread fallback

Use this only when the PR tool cannot list, reply to, or resolve review threads.
Target the repository that owns the PR explicitly; the checkout may be a fork.

Create each query and reply file below with a unique name in the OS temporary
directory, never in the repository. Delete it immediately after use.

## Fetch all unresolved feedback

Save the thread query:

```graphql
query(
  $owner: String!
  $repo: String!
  $number: Int!
  $endCursor: String
) {
  repository(owner: $owner, name: $repo) {
    pullRequest(number: $number) {
      reviewThreads(first: 100, after: $endCursor) {
        nodes { id isResolved }
        pageInfo { hasNextPage endCursor }
      }
    }
  }
}
```

Replace the uppercase tokens below with actual values and temporary-file paths.
Run the query with pagination and keep unresolved thread node IDs:

```text
gh api graphql --paginate -F owner=BASE_OWNER -F repo=BASE_REPO -F number=PULL_NUMBER -F query=@THREADS_QUERY_FILE --jq '.data.repository.pullRequest.reviewThreads.nodes[] | select(.isResolved == false) | .id'
```

For each unresolved thread, save and run this paginated comments query:

```graphql
query($threadId: ID!, $endCursor: String) {
  node(id: $threadId) {
    ... on PullRequestReviewThread {
      comments(first: 100, after: $endCursor) {
        nodes {
          id
          body
          url
          path
          line
          author { login }
        }
        pageInfo { hasNextPage endCursor }
      }
    }
  }
}
```

```text
gh api graphql --paginate -F threadId=THREAD_NODE_ID -F query=@COMMENTS_QUERY_FILE --jq '.data.node.comments.nodes[]'
```

Classify every unresolved thread after reading all its replies.

## Reply in-thread

Write the response to a unique temporary Markdown file, then use the thread node
ID to reply. This cannot create a top-level PR comment:

```graphql
mutation($threadId: ID!, $body: String!) {
  addPullRequestReviewThreadReply(
    input: { pullRequestReviewThreadId: $threadId, body: $body }
  ) {
    comment { id url body }
  }
}
```

```text
gh api graphql -F threadId=THREAD_NODE_ID -F body=@REPLY_FILE -F query=@REPLY_QUERY_FILE --jq '.data.addPullRequestReviewThreadReply.comment'
```

Require a non-null comment `id` before reporting that the reply was posted.

## Resolve separately

Save and run the resolve mutation:

```graphql
mutation($threadId: ID!) {
  resolveReviewThread(input: { threadId: $threadId }) {
    thread { id isResolved }
  }
}
```

```text
gh api graphql -F threadId=THREAD_NODE_ID -F query=@RESOLVE_QUERY_FILE --jq '.data.resolveReviewThread.thread'
```

Require `isResolved: true` before reporting success.
