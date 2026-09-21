import { createUseStyles } from "react-jss";

const useStyles = createUseStyles({
  card: {
    background: 'var(--white-color-hover)',
    padding: '4px',
    border: '1px solid #b2b2b2',
  },
});

export default function OldCard(props) {
  const s = useStyles();
  return <div className={s.card}>
    {props.children}
  </div>
}