// One retry timer per connection. Successful streams reset backoff; terminal/unmounted views stop permanently.
export function createRecoveryLoop(attempt: () => Promise<boolean>) {
  let timer: ReturnType<typeof setTimeout> | undefined
  let delay = 2000
  let stopped = false
  function reset() {
    clearTimeout(timer)
    timer = undefined
    delay = 2000
  }
  function schedule() {
    if (stopped || timer !== undefined) return
    timer = setTimeout(() => {
      timer = undefined
      delay = Math.min(delay * 2, 30000)
      void attempt()
        .then((recovered) => {
          if (!recovered) schedule()
        })
        .catch(schedule)
    }, delay)
  }
  return {
    schedule,
    reset,
    stop: () => {
      stopped = true
      reset()
    },
  }
}
