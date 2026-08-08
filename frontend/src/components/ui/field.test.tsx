import { describe, expect, it } from 'vitest';
import { render, screen, userEvent } from '@/test/render';
import { Field, FieldDescription, FieldLabel } from './field';
import { Input } from './input';

describe('Field + Input', () => {
  it('associates the label with the control, so getByLabelText finds it', () => {
    render(
      <Field>
        <FieldLabel>Admission number</FieldLabel>
        <Input name="admissionNumber" />
      </Field>,
    );

    expect(screen.getByLabelText('Admission number')).toBeInTheDocument();
  });

  it('focuses the control when the label is clicked', async () => {
    const user = userEvent.setup();
    render(
      <Field>
        <FieldLabel>Admission number</FieldLabel>
        <Input name="admissionNumber" />
      </Field>,
    );

    await user.click(screen.getByText('Admission number'));
    expect(screen.getByLabelText('Admission number')).toHaveFocus();
  });

  it('wires the description into the control accessible description', () => {
    render(
      <Field>
        <FieldLabel>Admission number</FieldLabel>
        <Input name="admissionNumber" />
        <FieldDescription>As printed on the ID card.</FieldDescription>
      </Field>,
    );

    expect(screen.getByLabelText('Admission number')).toHaveAccessibleDescription(
      'As printed on the ID card.',
    );
  });

  it('accepts typed input', async () => {
    const user = userEvent.setup();
    render(
      <Field>
        <FieldLabel>Admission number</FieldLabel>
        <Input name="admissionNumber" />
      </Field>,
    );

    const input = screen.getByLabelText('Admission number');
    await user.type(input, 'GRA/2026/0001');
    expect(input).toHaveValue('GRA/2026/0001');
  });
});

describe('Input', () => {
  it('applies the size variant', () => {
    render(<Input aria-label="Search" size="lg" />);
    expect(screen.getByLabelText('Search').className).toContain('h-11');
  });

  it('lets a caller className win over the default height', () => {
    render(<Input aria-label="Search" className="h-16" />);
    const className = screen.getByLabelText('Search').className;
    expect(className).toContain('h-16');
    expect(className).not.toContain('h-10');
  });

  it('supports being disabled', () => {
    render(<Input aria-label="Search" disabled />);
    expect(screen.getByLabelText('Search')).toBeDisabled();
  });
});
