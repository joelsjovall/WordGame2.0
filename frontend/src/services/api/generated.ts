// Generated from backend OpenAPI. Do not edit by hand.
export interface paths {
  '/api/v1/guests': {
    parameters: {
      query?: never
      header?: never
      path?: never
      cookie?: never
    }
    get?: never
    put?: never
    post: operations['EnsureGuestIdentity']
    delete?: never
    options?: never
    head?: never
    patch?: never
    trace?: never
  }
  '/api/v1/lobbies': {
    parameters: {
      query?: never
      header?: never
      path?: never
      cookie?: never
    }
    get?: never
    put?: never
    post: operations['CreatePrivateLobby']
    delete?: never
    options?: never
    head?: never
    patch?: never
    trace?: never
  }
  '/api/v1/lobbies/join': {
    parameters: {
      query?: never
      header?: never
      path?: never
      cookie?: never
    }
    get?: never
    put?: never
    post: operations['JoinPrivateLobby']
    delete?: never
    options?: never
    head?: never
    patch?: never
    trace?: never
  }
  '/api/v1/categories': {
    parameters: {
      query?: never
      header?: never
      path?: never
      cookie?: never
    }
    /** List category metadata, optionally filtered by difficulty. */
    get: operations['ListCategories']
    put?: never
    post?: never
    delete?: never
    options?: never
    head?: never
    patch?: never
    trace?: never
  }
  '/api/v1/categories/{categoryId}': {
    parameters: {
      query?: never
      header?: never
      path?: never
      cookie?: never
    }
    /** Get category metadata without accepted answers or aliases. */
    get: operations['GetCategory']
    put?: never
    post?: never
    delete?: never
    options?: never
    head?: never
    patch?: never
    trace?: never
  }
  '/api/v1/games/{gameId}/state': {
    parameters: {
      query?: never
      header?: never
      path?: never
      cookie?: never
    }
    get: operations['GetGameState']
    put?: never
    post?: never
    delete?: never
    options?: never
    head?: never
    patch?: never
    trace?: never
  }
  '/api/v1/games/{gameId}/events': {
    parameters: {
      query?: never
      header?: never
      path?: never
      cookie?: never
    }
    get: operations['WatchGameEvents']
    put?: never
    post?: never
    delete?: never
    options?: never
    head?: never
    patch?: never
    trace?: never
  }
  '/api/v1/lobbies/{lobbyId}': {
    parameters: {
      query?: never
      header?: never
      path?: never
      cookie?: never
    }
    get: operations['GetLobby']
    put?: never
    post?: never
    delete?: never
    options?: never
    head?: never
    patch?: never
    trace?: never
  }
  '/api/v1/lobbies/{lobbyId}/events': {
    parameters: {
      query?: never
      header?: never
      path?: never
      cookie?: never
    }
    get: operations['WatchLobby']
    put?: never
    post?: never
    delete?: never
    options?: never
    head?: never
    patch?: never
    trace?: never
  }
  '/api/v1/lobbies/{lobbyId}/settings': {
    parameters: {
      query?: never
      header?: never
      path?: never
      cookie?: never
    }
    get?: never
    put?: never
    post: operations['ConfigureLobby']
    delete?: never
    options?: never
    head?: never
    patch?: never
    trace?: never
  }
  '/api/v1/lobbies/{lobbyId}/start': {
    parameters: {
      query?: never
      header?: never
      path?: never
      cookie?: never
    }
    get?: never
    put?: never
    post: operations['StartLobby']
    delete?: never
    options?: never
    head?: never
    patch?: never
    trace?: never
  }
}
export type webhooks = Record<string, never>
export interface components {
  schemas: {
    /** @enum {unknown} */
    ApiCategoryDifficulty: 'easy' | 'medium' | 'hard'
    /** @enum {unknown} */
    ApiChallengeOutcome: 'pending' | 'succeeded' | 'failed' | 'cancelled'
    /** @enum {unknown} */
    ApiEventKind: 'updated' | 'removed'
    /** @enum {unknown} */
    ApiLobbyStatus: 'waiting' | 'starting' | 'started'
    /** @enum {unknown} */
    ApiMatchFormat: 'party' | 'online'
    ApiProblemDetails: {
      type?: null | string
      title?: null | string
      /** Format: int32 */
      status?: null | number
      detail?: null | string
      instance?: null | string
      traceId: string
      errors?: null | {
        [key: string]: string[]
      }
    }
    /** @enum {unknown} */
    ApiRoundStatus:
      'categorySelection' | 'bidding' | 'challenge' | 'completed' | 'cancelled'
    /** @enum {unknown} */
    ApiSessionStatus:
      'waiting' | 'starting' | 'active' | 'completed' | 'abandoned'
    /** @enum {unknown} */
    ApiSubmissionStatus: 'accepted' | 'rejected' | 'duplicate'
    BidResponse: {
      /** Format: uuid */
      playerId: string
      /** Format: int32 */
      wordCount: number
    }
    CategoryListResponse: {
      items: components['schemas']['CategoryResponse'][]
    }
    CategoryResponse: {
      id: string
      name: string
      difficulty: components['schemas']['ApiCategoryDifficulty']
    }
    ChallengeResponse: {
      /** Format: uuid */
      id: string
      /** Format: uuid */
      playerId: string
      /** Format: uuid */
      callerId: string
      /** Format: int32 */
      requiredWordCount: number
      /** Format: date-time */
      startedAt: string
      /** Format: date-time */
      deadline: string
      outcome: components['schemas']['ApiChallengeOutcome']
      /** Format: int32 */
      acceptedWordCount: number
      submissions: components['schemas']['SubmittedWordResponse'][]
    }
    CreateGameRequest: {
      /** Format: uuid */
      lobbyId: string
    }
    CreateGuestRequest: Record<string, never>
    CreateLobbyRequest: {
      /** Format: uuid */
      requestId: string
      displayName: string
    }
    GameActionRequest:
      | {
          /** @enum {string} */
          type: 'selectCategory'
          categoryId: string
          /** Format: uuid */
          actionId: string
          expectedVersion: string
        }
      | {
          /** @enum {string} */
          type: 'placeBid'
          /** Format: int32 */
          wordCount: number
          /** Format: uuid */
          actionId: string
          expectedVersion: string
        }
      | {
          /** @enum {string} */
          type: 'callBluff'
          /** Format: uuid */
          actionId: string
          expectedVersion: string
        }
      | {
          /** @enum {string} */
          type: 'submitWord'
          word: string
          /** Format: uuid */
          actionId: string
          expectedVersion: string
        }
    GameActionResponse: {
      /** Format: uuid */
      actionId: string
      version: string
    }
    GameCreatedResponse: {
      /** Format: uuid */
      gameId: string
    }
    GameEventResponse: {
      /** Format: uuid */
      gameId: string
      version: string
      status: components['schemas']['ApiSessionStatus']
      kind: components['schemas']['ApiEventKind']
    }
    GamePlayerResponse: {
      /** Format: uuid */
      id: string
      displayName: string
      /** Format: int32 */
      score: number
      isConnected: boolean
    }
    GameRoundResponse: {
      /** Format: uuid */
      id: string
      /** Format: int32 */
      number: number
      status: components['schemas']['ApiRoundStatus']
      /** Format: uuid */
      activePlayerId: string
      category: null | components['schemas']['CategoryResponse']
      highestBid: null | components['schemas']['BidResponse']
      challenge: null | components['schemas']['ChallengeResponse']
      result?: null | components['schemas']['RoundResultResponse']
      /** Format: date-time */
      categorySelectionDeadline?: null | string
    }
    GameStateResponse: {
      /** Format: uuid */
      id: string
      status: components['schemas']['ApiSessionStatus']
      version: string
      /** Format: date-time */
      serverTime: string
      players: components['schemas']['GamePlayerResponse'][]
      round: null | components['schemas']['GameRoundResponse']
      match: components['schemas']['MatchSettingsResponse']
      result: null | components['schemas']['MatchResultResponse']
    }
    GuestIdentityResponse: {
      /** Format: uuid */
      playerId: string
    }
    JoinLobbyRequest: {
      lobbyCode: string
      displayName: string
      /** Format: uuid */
      lobbyId?: null | string
    }
    LobbyPlayerResponse: {
      /** Format: uuid */
      id: string
      displayName: string
    }
    LobbyResponse: {
      /** Format: uuid */
      id: string
      lobbyCode: string
      /** Format: uuid */
      hostPlayerId: string
      status: components['schemas']['ApiLobbyStatus']
      players: components['schemas']['LobbyPlayerResponse'][]
      /** Format: int32 */
      minimumPlayers: number
      /** Format: int32 */
      maximumPlayers: number
      /** Format: date-time */
      createdAt: string
      /** Format: date-time */
      expiresAt: string
      version: string
      settings: components['schemas']['LobbySettingsRequest']
      /** Format: uuid */
      gameId: null | string
    }
    LobbySettingsRequest: {
      /** Format: int32 */
      maximumPlayers: number
      /** Format: int32 */
      roundCount: null | number
      /** Format: int32 */
      challengeSeconds: number
      allowedDifficulties: components['schemas']['ApiCategoryDifficulty'][]
    }
    MatchResultResponse: {
      /** Format: uuid */
      gameId: string
      format: components['schemas']['ApiMatchFormat']
      /** Format: int32 */
      completedRoundCount: number
      isTie: boolean
      winnerIds: string[]
      standings: components['schemas']['MatchStandingResponse'][]
    }
    MatchSettingsResponse: {
      format: components['schemas']['ApiMatchFormat']
      /** Format: int32 */
      roundCount: number
      /** Format: double */
      challengeSeconds?: null | number
      allowedDifficulties?:
        null | components['schemas']['ApiCategoryDifficulty'][]
    }
    MatchStandingResponse: {
      /** Format: uuid */
      playerId: string
      displayName: string
      /** Format: int32 */
      score: number
      /** Format: int32 */
      rank: number
    }
    RoundPlayerScoreResponse: {
      /** Format: uuid */
      playerId: string
      /** Format: int32 */
      pointsAwarded: number
      /** Format: int32 */
      totalPoints: number
    }
    RoundResultResponse: {
      /** Format: uuid */
      roundId: string
      /** Format: int32 */
      roundNumber: number
      category: components['schemas']['CategoryResponse']
      /** Format: uuid */
      challengeId: string
      /** Format: uuid */
      bidderId: string
      /** Format: uuid */
      callerId: string
      /** Format: uuid */
      winnerId: string
      /** Format: int32 */
      requiredWordCount: number
      /** Format: int32 */
      acceptedWordCount: number
      outcome: components['schemas']['ApiChallengeOutcome']
      submissions: components['schemas']['SubmittedWordResponse'][]
      scores: components['schemas']['RoundPlayerScoreResponse'][]
    }
    StartLobbyRequest: {
      expectedVersion: string
    }
    SubmittedWordResponse: {
      text: string
      status: components['schemas']['ApiSubmissionStatus']
    }
    UpdateLobbySettingsRequest: {
      expectedVersion: string
      settings: components['schemas']['LobbySettingsRequest']
    }
  }
  responses: never
  parameters: never
  requestBodies: never
  headers: never
  pathItems: never
}
export type $defs = Record<string, never>
export interface operations {
  EnsureGuestIdentity: {
    parameters: {
      query?: never
      header?: never
      path?: never
      cookie?: never
    }
    requestBody: {
      content: {
        'application/json': components['schemas']['CreateGuestRequest']
      }
    }
    responses: {
      /** @description OK */
      200: {
        headers: {
          [name: string]: unknown
        }
        content: {
          'application/json': components['schemas']['GuestIdentityResponse']
        }
      }
      /** @description Bad Request */
      400: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
      /** @description Forbidden */
      403: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
    }
  }
  CreatePrivateLobby: {
    parameters: {
      query?: never
      header?: never
      path?: never
      cookie?: never
    }
    requestBody: {
      content: {
        'application/json': components['schemas']['CreateLobbyRequest']
      }
    }
    responses: {
      /** @description OK */
      200: {
        headers: {
          [name: string]: unknown
        }
        content: {
          'application/json': components['schemas']['LobbyResponse']
        }
      }
      /** @description Created */
      201: {
        headers: {
          [name: string]: unknown
        }
        content: {
          'application/json': components['schemas']['LobbyResponse']
        }
      }
      /** @description Bad Request */
      400: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
      /** @description Unauthorized */
      401: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
      /** @description Forbidden */
      403: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
      /** @description Conflict */
      409: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
    }
  }
  JoinPrivateLobby: {
    parameters: {
      query?: never
      header?: never
      path?: never
      cookie?: never
    }
    requestBody: {
      content: {
        'application/json': components['schemas']['JoinLobbyRequest']
      }
    }
    responses: {
      /** @description OK */
      200: {
        headers: {
          [name: string]: unknown
        }
        content: {
          'application/json': components['schemas']['LobbyResponse']
        }
      }
      /** @description Bad Request */
      400: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
      /** @description Unauthorized */
      401: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
      /** @description Forbidden */
      403: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
      /** @description Not Found */
      404: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
      /** @description Conflict */
      409: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
    }
  }
  ListCategories: {
    parameters: {
      query?: {
        difficulty?: string
      }
      header?: never
      path?: never
      cookie?: never
    }
    requestBody?: never
    responses: {
      /** @description OK */
      200: {
        headers: {
          [name: string]: unknown
        }
        content: {
          'application/json': components['schemas']['CategoryListResponse']
        }
      }
      /** @description Bad Request */
      400: {
        headers: {
          [name: string]: unknown
        }
        content: {
          'application/problem+json': components['schemas']['ApiProblemDetails']
        }
      }
      /** @description Internal Server Error */
      500: {
        headers: {
          [name: string]: unknown
        }
        content: {
          'application/problem+json': components['schemas']['ApiProblemDetails']
        }
      }
    }
  }
  GetCategory: {
    parameters: {
      query?: never
      header?: never
      path: {
        categoryId: string
      }
      cookie?: never
    }
    requestBody?: never
    responses: {
      /** @description OK */
      200: {
        headers: {
          [name: string]: unknown
        }
        content: {
          'application/json': components['schemas']['CategoryResponse']
        }
      }
      /** @description Not Found */
      404: {
        headers: {
          [name: string]: unknown
        }
        content: {
          'application/problem+json': components['schemas']['ApiProblemDetails']
        }
      }
      /** @description Internal Server Error */
      500: {
        headers: {
          [name: string]: unknown
        }
        content: {
          'application/problem+json': components['schemas']['ApiProblemDetails']
        }
      }
    }
  }
  GetGameState: {
    parameters: {
      query?: never
      header?: never
      path: {
        gameId: string
      }
      cookie?: never
    }
    requestBody?: never
    responses: {
      /** @description OK */
      200: {
        headers: {
          [name: string]: unknown
        }
        content: {
          'application/json': components['schemas']['GameStateResponse']
        }
      }
      /** @description Unauthorized */
      401: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
      /** @description Not Found */
      404: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
    }
  }
  WatchGameEvents: {
    parameters: {
      query?: never
      header?: never
      path: {
        gameId: string
      }
      cookie?: never
    }
    requestBody?: never
    responses: {
      /** @description OK */
      200: {
        headers: {
          [name: string]: unknown
        }
        content: {
          'text/event-stream': components['schemas']['GameEventResponse']
        }
      }
      /** @description Unauthorized */
      401: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
      /** @description Not Found */
      404: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
    }
  }
  GetLobby: {
    parameters: {
      query?: never
      header?: never
      path: {
        lobbyId: string
      }
      cookie?: never
    }
    requestBody?: never
    responses: {
      /** @description OK */
      200: {
        headers: {
          [name: string]: unknown
        }
        content: {
          'application/json': components['schemas']['LobbyResponse']
        }
      }
      /** @description Unauthorized */
      401: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
      /** @description Not Found */
      404: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
    }
  }
  WatchLobby: {
    parameters: {
      query?: never
      header?: never
      path: {
        lobbyId: string
      }
      cookie?: never
    }
    requestBody?: never
    responses: {
      /** @description OK */
      200: {
        headers: {
          [name: string]: unknown
        }
        content: {
          'text/event-stream': components['schemas']['LobbyResponse']
        }
      }
      /** @description Unauthorized */
      401: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
      /** @description Not Found */
      404: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
    }
  }
  ConfigureLobby: {
    parameters: {
      query?: never
      header?: never
      path: {
        lobbyId: string
      }
      cookie?: never
    }
    requestBody: {
      content: {
        'application/json': components['schemas']['UpdateLobbySettingsRequest']
      }
    }
    responses: {
      /** @description OK */
      200: {
        headers: {
          [name: string]: unknown
        }
        content: {
          'application/json': components['schemas']['LobbyResponse']
        }
      }
      /** @description Bad Request */
      400: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
      /** @description Unauthorized */
      401: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
      /** @description Forbidden */
      403: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
      /** @description Not Found */
      404: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
      /** @description Conflict */
      409: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
    }
  }
  StartLobby: {
    parameters: {
      query?: never
      header?: never
      path: {
        lobbyId: string
      }
      cookie?: never
    }
    requestBody: {
      content: {
        'application/json': components['schemas']['StartLobbyRequest']
      }
    }
    responses: {
      /** @description OK */
      200: {
        headers: {
          [name: string]: unknown
        }
        content: {
          'application/json': components['schemas']['LobbyResponse']
        }
      }
      /** @description Bad Request */
      400: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
      /** @description Unauthorized */
      401: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
      /** @description Forbidden */
      403: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
      /** @description Not Found */
      404: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
      /** @description Conflict */
      409: {
        headers: {
          [name: string]: unknown
        }
        content?: never
      }
    }
  }
}
