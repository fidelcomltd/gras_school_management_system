import { useEffect } from 'react';
import type { InfiniteData, UseInfiniteQueryResult } from '@tanstack/react-query';

/**
 * Every item of a cursor-paginated list, fetching page after page until there are no more. For a picker or a name lookup,
 * which must see the whole list, never the first page (the pattern `useActiveArms` started).
 */
export function useAllPages<T>(query: UseInfiniteQueryResult<InfiniteData<{ items: T[] }>>, enabled = true): T[] {
  const { hasNextPage, isFetchingNextPage, fetchNextPage } = query;
  useEffect(() => {
    if (enabled && hasNextPage && !isFetchingNextPage) void fetchNextPage();
  }, [enabled, hasNextPage, isFetchingNextPage, fetchNextPage]);
  return query.data?.pages.flatMap((page) => page.items) ?? [];
}
