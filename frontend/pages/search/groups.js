import SearchGroups from "../../components/searchGroups";
import {useRouter} from "next/router";
import Theme2016 from "../../components/theme2016";

const SearchGroupsPage = props => {
  const router = useRouter();
  return <Theme2016>
      <SearchGroups keyword={router.query.keyword} />
  </Theme2016>
}

export default SearchGroupsPage;