import { useState } from 'react';
import { LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { useMe } from '@/features/auth/api';
import { useSections } from '@/features/classes/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { cn } from '@/lib/utils/cn';
import { useSettings } from './api';
import { useResultRules } from './api-groups';
import { AssessmentPanel } from './components/assessment-panel';
import { DevelopmentDomainsEditor } from './components/development-domains-editor';
import { GradingPanel } from './components/grading-panel';
import { IdentityPanel } from './components/identity-panel';
import { RatingScalesEditor } from './components/rating-scales-editor';
import { RegNumberPanel } from './components/reg-number-panel';
import { ResultRulesPanel } from './components/result-rules-panel';
import { TraitsEditor } from './components/traits-editor';

const TABS = [
  { id: 'identity', label: 'School' },
  { id: 'regNumber', label: 'Registration numbers' },
  { id: 'grading', label: 'Grading' },
  { id: 'assessment', label: 'Assessment' },
  { id: 'rules', label: 'Result rules' },
  { id: 'ratings', label: 'Ratings' },
] as const;

type TabId = (typeof TABS)[number]['id'];

/**
 * `/settings` (spec 6.2), behind `settings.view`. One tab per settings group; each group saves on its own with the
 * version it was read at. Each editor is keyed by its group's version so a save starts again from the server's values.
 */
export function SettingsScreen() {
  const settings = useSettings();
  const rules = useResultRules();
  const sections = useSections();
  const me = useMe();
  const [tab, setTab] = useState<TabId>('identity');
  const can = (privilege: string) => !!me.data && hasPrivilege(me.data, privilege);

  if (settings.isPending) return <LoadingState label="Loading settings…" />;
  if (settings.isError) return <QueryErrorState error={settings.error} onRetry={() => void settings.refetch()} />;
  const data = settings.data;

  const panel = () => {
    switch (tab) {
      case 'identity':
        return <IdentityPanel identity={data.identity} />;
      case 'regNumber':
        return (
          <RegNumberPanel
            key={`${data.regNumber.versionNumber}:${data.abbreviation.versionNumber}`}
            settings={data}
            canEditFormat={can('settings.regnumber.update')}
            canEditAbbreviation={can('settings.abbreviation.update')}
          />
        );
      case 'grading':
        return <GradingPanel key={data.grading.versionNumber} grading={data.grading} canEdit={can('settings.grading.update')} />;
      case 'assessment':
        return <AssessmentPanel key={data.assessment.versionNumber} assessment={data.assessment} canEdit={can('settings.assessment.update')} />;
      case 'rules':
        if (rules.isPending) return <LoadingState label="Loading result rules…" />;
        if (rules.isError) return <QueryErrorState error={rules.error} onRetry={() => void rules.refetch()} />;
        return <ResultRulesPanel key={rules.data.versionNumber} rules={rules.data} canEdit={can('settings.resultrules.update')} />;
      case 'ratings': {
        const scales = data.ratingScales.scales;
        return (
          <div className="flex max-w-3xl flex-col gap-8">
            <RatingScalesEditor key={`scales:${data.ratingScales.versionNumber}`} group={data.ratingScales} canEdit={can('settings.ratingscales.update')} />
            <TraitsEditor key={`traits:${data.traits.versionNumber}`} group={data.traits} scales={scales} canEdit={can('settings.traits.update')} />
            <DevelopmentDomainsEditor
              key={`domains:${data.developmentDomains.versionNumber}`}
              group={data.developmentDomains}
              sections={sections.data?.sections ?? []}
              scales={scales}
              canEdit={can('settings.developmentdomains.update')}
            />
          </div>
        );
      }
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-col gap-1">
        <h1 className="font-display text-2xl font-semibold text-foreground">School settings</h1>
        <p className="text-sm text-muted-foreground">How the school appears on result sheets and pin slips, and how results are graded and computed.</p>
      </header>

      <div role="tablist" aria-label="Settings" className="flex flex-wrap gap-1 border-b border-border">
        {TABS.map((t) => (
          <button
            key={t.id}
            type="button"
            role="tab"
            id={`settings-tab-${t.id}`}
            aria-selected={tab === t.id}
            aria-controls="settings-panel"
            className={cn(
              '-mb-px border-b-2 px-3 py-2 text-sm font-medium',
              tab === t.id ? 'border-primary text-primary' : 'border-transparent text-muted-foreground hover:text-foreground',
            )}
            onClick={() => setTab(t.id)}
          >
            {t.label}
          </button>
        ))}
      </div>

      <div role="tabpanel" id="settings-panel" aria-labelledby={`settings-tab-${tab}`}>
        {panel()}
      </div>
    </div>
  );
}
