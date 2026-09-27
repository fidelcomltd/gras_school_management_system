import { Button } from './button';
import { Spinner } from './spinner';

/** The "Load more" at the foot of a cursor-paged list, with a spinner while the next page loads. */
export function LoadMoreButton({ loading, onClick }: { loading: boolean; onClick: () => void }) {
  return (
    <Button variant="outline" size="sm" className="self-center" disabled={loading} onClick={onClick}>
      {loading ? (
        <>
          <Spinner className="size-4" />
          Loading…
        </>
      ) : (
        'Load more'
      )}
    </Button>
  );
}
