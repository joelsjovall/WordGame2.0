// Generated from backend OpenAPI. Do not edit by hand.
export interface paths {
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
}
export type webhooks = Record<string, never>
export interface components {
  schemas: {
    /** @enum {unknown} */
    ApiCategoryDifficulty: 'easy' | 'medium' | 'hard'
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
    CategoryListResponse: {
      items: components['schemas']['CategoryResponse'][]
    }
    CategoryResponse: {
      id: string
      name: string
      difficulty: components['schemas']['ApiCategoryDifficulty']
    }
    CreateGameRequest: {
      /** Format: uuid */
      lobbyId: string
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
      /** @enum {unknown} */
      status: 'waiting' | 'starting' | 'active' | 'completed' | 'abandoned'
      /** @enum {unknown} */
      kind: 'updated' | 'removed'
    }
    GameStateResponse: {
      /** Format: uuid */
      id: string
      /** @enum {unknown} */
      status: 'waiting' | 'starting' | 'active' | 'completed' | 'abandoned'
      version: string
      /** Format: date-time */
      serverTime: string
      players: {
        /** Format: uuid */
        id: string
        displayName: string
        /** Format: int32 */
        score: number
        isConnected: boolean
      }[]
      round: null | {
        /** Format: uuid */
        id: string
        /** Format: int32 */
        number: number
        /** @enum {unknown} */
        status:
          | 'categorySelection'
          | 'bidding'
          | 'challenge'
          | 'completed'
          | 'cancelled'
        /** Format: uuid */
        activePlayerId: string
        category: null | {
          id: string
          name: string
          /** @enum {unknown} */
          difficulty: 'easy' | 'medium' | 'hard'
        }
        highestBid: null | {
          /** Format: uuid */
          playerId: string
          /** Format: int32 */
          wordCount: number
        }
        challenge: null | {
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
          /** @enum {unknown} */
          outcome: 'pending' | 'succeeded' | 'failed' | 'cancelled'
          /** Format: int32 */
          acceptedWordCount: number
          submissions: {
            text: string
            /** @enum {unknown} */
            status: 'accepted' | 'rejected' | 'duplicate'
          }[]
        }
      }
    }
    JoinGameRequest: {
      lobbyCode: string
    }
    JoinGameResponse: {
      /** Format: uuid */
      lobbyId: string
      lobbyCode: string
      /** Format: uuid */
      gameId: null | string
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
}
